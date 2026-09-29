using System;
using System.Collections.Generic;
using System.Reflection;
using GK2.MapMarkers.Api;
using GK2.MapMarkers.Providers;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2.MapMarkers.Map
{
    /// <summary>
    /// Lives under MapPageWidget's map rect. Rebuilds markers when the map is redrawn and then
    /// periodically while it stays open, so moving NPCs are tracked live.
    /// <para>
    /// Fog clouds are UISortComponents under the widget's UIHierarchySorter, drawn back to front by their floor line
    /// (y + FloorLine). Tags normally sit below all clouds; a tag whose tip is in front of (south of) every cloud it
    /// overlaps moves to a second layer above the clouds, so revealed places are not hidden by fog from the north.
    /// </para>
    /// </summary>
    internal sealed class MapOverlay : MonoBehaviour
    {
        private const string ObjectName = "GK2MapMarkersOverlay";
        private const string FrontObjectName = "GK2MapMarkersFront";
        private const float ClusterGapPixels = 1f;   // in art pixels
        private const float ClusterLiftPixels = 6f;  // in art pixels

        private static readonly FieldInfo MapRectField = AccessTools.Field(typeof(MapPageWidget), "mapRect");
        private static readonly FieldInfo PlayerIconField = AccessTools.Field(typeof(MapPageWidget), "playerIcon");
        private static readonly FieldInfo VirtualCursorField = AccessTools.Field(typeof(MapPageWidget), "mapVirtualCursor");
        private static readonly FieldInfo HierarchySorterField = AccessTools.Field(typeof(MapPageWidget), "hierarchySorter");
        private static readonly Vector3[] CornerBuffer = new Vector3[4];
        private static readonly List<MapOverlay> Instances = new List<MapOverlay>();

        private readonly Dictionary<string, MarkerView> activeViews = new Dictionary<string, MarkerView>();
        private readonly Stack<MarkerView> pool = new Stack<MarkerView>();
        private readonly List<MapMarker> markers = new List<MapMarker>();
        private readonly List<WgoData> wgoBuffer = new List<WgoData>();
        private readonly Dictionary<string, MarkerLabelMode> labelModes = new Dictionary<string, MarkerLabelMode>();
        private readonly HashSet<string> seenKeys = new HashSet<string>();
        private readonly HashSet<string> failedProviders = new HashSet<string>();
        private readonly List<LeaderView> leaders = new List<LeaderView>();
        private readonly List<MarkerDeclutter.Item> placements = new List<MarkerDeclutter.Item>();
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<Cloud> clouds = new List<Cloud>();
        private readonly List<UISortComponent> sortBuffer = new List<UISortComponent>();
        private readonly List<Graphic> graphicBuffer = new List<Graphic>();

        private struct Cloud
        {
            public Rect Bounds;
            public float FloorY;
        }

        private sealed class Entry
        {
            public MapMarker Marker;
            public Sprite Art;
            public string Key;
            public MarkerDeclutter.Item Placement;
        }

        private MapPageWidget widget;
        private RectTransform mapRect;
        private RectTransform playerIcon;
        private RectTransform container;
        private RectTransform leadersRoot;
        private RectTransform tagsRoot;
        private RectTransform frontContainer;
        private RectTransform frontTagsRoot;
        private RectTransform cloudsRoot;
        private TMP_Text fontTemplate;
        private MapVirtualCursor virtualCursor;
        private MarkerView gamepadSelected;
        private float timer;
        private bool dirty = true;

        public static void AttachAndRefresh(MapPageWidget widget)
        {
            if (widget == null || MapRectField == null)
            {
                return;
            }
            var mapRect = MapRectField.GetValue(widget) as RectTransform;
            if (mapRect == null)
            {
                return;
            }

            Transform existing = mapRect.Find(ObjectName);
            MapOverlay overlay = existing != null ? existing.GetComponent<MapOverlay>() : null;
            if (overlay == null)
            {
                var go = new GameObject(ObjectName, typeof(RectTransform));
                go.layer = mapRect.gameObject.layer;
                overlay = go.AddComponent<MapOverlay>();
                overlay.Init(widget, mapRect);
            }
            overlay.Refresh();
        }

        public static void RequestRefreshAll()
        {
            foreach (MapOverlay overlay in Instances)
            {
                overlay.dirty = true;
            }
        }

        public static void DestroyAll()
        {
            foreach (MapOverlay overlay in Instances.ToArray())
            {
                if (overlay != null)
                {
                    Destroy(overlay.gameObject);
                }
            }
            Instances.Clear();
        }

        private void Init(MapPageWidget owner, RectTransform map)
        {
            widget = owner;
            mapRect = map;
            playerIcon = PlayerIconField?.GetValue(owner) as RectTransform;
            virtualCursor = VirtualCursorField?.GetValue(owner) as MapVirtualCursor;
            cloudsRoot = (HierarchySorterField?.GetValue(owner) as UIHierarchySorter)?.HierarchyTarget;

            container = (RectTransform)transform;
            SetUpContainer(container, mapRect);
            // Leader lines are drawn below all tags.
            leadersRoot = CreateLayer(container, "Leaders");
            tagsRoot = CreateLayer(container, "Tags");

            var front = new GameObject(FrontObjectName, typeof(RectTransform));
            front.layer = gameObject.layer;
            frontContainer = (RectTransform)front.transform;
            SetUpContainer(frontContainer, mapRect);
            frontTagsRoot = CreateLayer(frontContainer, "Tags");

            foreach (TMP_Text text in owner.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font != null)
                {
                    fontTemplate = text;
                    break;
                }
            }
        }

        private static void SetUpContainer(RectTransform rect, RectTransform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private RectTransform CreateLayer(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var layer = (RectTransform)go.transform;
            layer.SetParent(parent, false);
            layer.anchorMin = layer.anchorMax = layer.pivot = new Vector2(0.5f, 0.5f);
            layer.anchoredPosition = Vector2.zero;
            layer.sizeDelta = Vector2.zero;
            return layer;
        }

        private void OnEnable()
        {
            if (!Instances.Contains(this))
            {
                Instances.Add(this);
            }
            dirty = true;
        }

        private void OnDestroy()
        {
            Instances.Remove(this);
            if (frontContainer != null)
            {
                Destroy(frontContainer.gameObject);
            }
        }

        private void Update()
        {
            UpdateGamepadSelection();

            // Hold the layout while a tag is hovered: re-spreading moving NPCs would slide the tag out from
            // under the pointer and hide its name.
            if (!dirty && IsAnyHovered())
            {
                return;
            }
            timer += Time.unscaledDeltaTime;
            if (dirty || timer >= Plugin.Settings.RefreshInterval.Value)
            {
                Refresh();
            }
        }

        /// <summary>
        /// With a gamepad the map uses a virtual cursor (MapVirtualCursor) instead of pointer events. The game selects
        /// milestones through 2D trigger colliders; markers instead hit-test the cursor position against their tag
        /// rectangles, then show the label and snap the cursor frame onto the tag like a milestone.
        /// </summary>
        private void UpdateGamepadSelection()
        {
            MarkerView hit = null;
            if (virtualCursor != null && virtualCursor.isActiveAndEnabled && LazyInput.IsGamepadActive)
            {
                Vector3 cursorPosition = virtualCursor.transform.position;
                hit = HitTest(frontTagsRoot, cursorPosition) ?? HitTest(tagsRoot, cursorPosition);
            }

            if (hit == gamepadSelected)
            {
                return;
            }
            if (gamepadSelected != null)
            {
                gamepadSelected.SetGamepadHover(false);
            }
            if (hit != null)
            {
                hit.SetGamepadHover(true);
            }
            if (virtualCursor != null && virtualCursor.isActiveAndEnabled)
            {
                virtualCursor.DoAnimationTo(hit != null ? hit.NavigationRect : null);
            }
            gamepadSelected = hit;
        }

        private static MarkerView HitTest(RectTransform layer, Vector3 worldPosition)
        {
            // Topmost first: later siblings are drawn above earlier ones.
            for (int i = layer.childCount - 1; i >= 0; i--)
            {
                var tag = (RectTransform)layer.GetChild(i);
                if (!tag.gameObject.activeSelf)
                {
                    continue;
                }
                Vector2 local = tag.InverseTransformPoint(worldPosition);
                if (tag.rect.Contains(local))
                {
                    return tag.GetComponent<MarkerView>();
                }
            }
            return null;
        }

        private bool IsAnyHovered()
        {
            foreach (MarkerView view in activeViews.Values)
            {
                if (view != null && view.IsHovered)
                {
                    return true;
                }
            }
            return false;
        }

        public void Refresh()
        {
            timer = 0f;
            dirty = false;

            if (!Plugin.IsActive || widget == null || !WgoUtil.TryCollectAll(wgoBuffer))
            {
                HideAll();
                return;
            }

            MapProjection projection = MapProjection.TryCreate(widget, mapRect);
            if (projection == null)
            {
                HideAll();
                return;
            }

            container.sizeDelta = mapRect.sizeDelta;
            frontContainer.sizeDelta = mapRect.sizeDelta;
            CollectMarkers();
            CollectClouds();
            Layout(projection);
            KeepPlayerOnTop();
            KeepFrontAboveClouds();
        }

        private void CollectMarkers()
        {
            markers.Clear();
            labelModes.Clear();
            var context = new MapMarkerContext(wgoBuffer);
            foreach (IMapMarkerProvider provider in MapMarkersApi.Providers)
            {
                try
                {
                    if (!provider.IsEnabled)
                    {
                        continue;
                    }
                    labelModes[provider.Id] = provider.LabelMode;
                    provider.Collect(context, markers);
                }
                catch (Exception e)
                {
                    // Log once per provider; a broken extension must not break the map.
                    if (failedProviders.Add(provider.Id))
                    {
                        Plugin.Log?.Error($"Provider '{provider.Id}' failed: {e}");
                    }
                }
            }
            markers.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        }

        private void Layout(MapProjection projection)
        {
            seenKeys.Clear();
            entries.Clear();
            placements.Clear();
            float pixel = GetUnitsPerArtPixel();

            foreach (MapMarker marker in markers)
            {
                if (!projection.TryProject(marker.WorldPosition, marker.WorldZoneId, out Vector2 anchor, out _))
                {
                    continue;
                }
                string key = marker.CategoryId + ":" + marker.Key;
                if (!seenKeys.Add(key))
                {
                    continue;
                }
                Sprite art = GetArt(marker);
                if (art == null)
                {
                    continue;
                }
                var placement = new MarkerDeclutter.Item
                {
                    Anchor = anchor,
                    Size = MarkerView.GetSize(art, pixel, marker.Scale),
                    Group = marker.CategoryId
                };
                placements.Add(placement);
                entries.Add(new Entry { Marker = marker, Art = art, Key = key, Placement = placement });
            }

            if (Plugin.Settings.SpreadOverlapping.Value)
            {
                // Overlapping tags (including NPCs sharing one interior anchor) are spread into rows with leader lines.
                MarkerDeclutter.Resolve(placements, ClusterGapPixels * pixel, ClusterLiftPixels * pixel);
            }
            else
            {
                foreach (MarkerDeclutter.Item placement in placements)
                {
                    placement.Tip = placement.Anchor;
                    placement.Displaced = false;
                }
            }

            // Category order first, then top-to-bottom so lower tags overlap the ones above them.
            entries.Sort((x, y) =>
            {
                int bySort = x.Marker.SortOrder.CompareTo(y.Marker.SortOrder);
                return bySort != 0 ? bySort : y.Placement.Tip.y.CompareTo(x.Placement.Tip.y);
            });

            MarkerView hovered = null;
            int leaderCount = 0;
            foreach (Entry entry in entries)
            {
                if (!activeViews.TryGetValue(entry.Key, out MarkerView view) || view == null)
                {
                    view = pool.Count > 0 ? pool.Pop() : MarkerView.Create(tagsRoot, fontTemplate);
                    activeViews[entry.Key] = view;
                }
                labelModes.TryGetValue(entry.Marker.CategoryId, out MarkerLabelMode mode);
                view.Apply(entry.Marker, entry.Art, mode, entry.Placement.Tip, pixel);
                // Both layers share one coordinate frame, so the anchored position survives the move.
                Transform layer = IsInFrontOfClouds(view) ? frontTagsRoot : tagsRoot;
                if (view.transform.parent != layer)
                {
                    view.transform.SetParent(layer, false);
                }
                view.transform.SetAsLastSibling(); // keeps the sort above as draw order
                if (view.IsHovered)
                {
                    hovered = view;
                }

                if (entry.Placement.Displaced)
                {
                    if (leaderCount == leaders.Count)
                    {
                        leaders.Add(LeaderView.Create(leadersRoot));
                    }
                    leaders[leaderCount++].Apply(entry.Placement.Tip, entry.Placement.Anchor, pixel, entry.Marker.Faded);
                }
            }

            // The tag under the pointer stays on top so its name label is not covered.
            if (hovered != null)
            {
                hovered.transform.SetAsLastSibling();
            }
            for (int i = leaderCount; i < leaders.Count; i++)
            {
                leaders[i].gameObject.SetActive(false);
            }

            ReleaseUnseen();
        }

        /// <summary>Active fog clouds with their world bounds and floor line, as UIHierarchySorter orders them.</summary>
        private void CollectClouds()
        {
            clouds.Clear();
            if (cloudsRoot == null || !Plugin.Settings.CloudDepthSort.Value)
            {
                return;
            }
            cloudsRoot.GetComponentsInChildren(false, sortBuffer);
            foreach (UISortComponent sort in sortBuffer)
            {
                if (TryGetWorldBounds(sort.transform, out Rect bounds))
                {
                    clouds.Add(new Cloud { Bounds = bounds, FloorY = sort.transform.position.y + sort.FloorLine });
                }
            }
        }

        private bool TryGetWorldBounds(Transform root, out Rect bounds)
        {
            bool any = false;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            root.GetComponentsInChildren(false, graphicBuffer);
            foreach (Graphic graphic in graphicBuffer)
            {
                if (!graphic.enabled)
                {
                    continue;
                }
                graphic.rectTransform.GetWorldCorners(CornerBuffer);
                xMin = Mathf.Min(xMin, CornerBuffer[0].x);
                yMin = Mathf.Min(yMin, CornerBuffer[0].y);
                xMax = Mathf.Max(xMax, CornerBuffer[2].x);
                yMax = Mathf.Max(yMax, CornerBuffer[2].y);
                any = true;
            }
            bounds = any ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : default;
            return any;
        }

        /// <summary>
        /// True when the tag overlaps a cloud and its tip is in front of every cloud it overlaps. A tag that touches no
        /// cloud stays in the lower layer so the player icon keeps drawing above it.
        /// </summary>
        private bool IsInFrontOfClouds(MarkerView view)
        {
            if (clouds.Count == 0)
            {
                return false;
            }
            Rect tag = view.GetWorldRect();
            float floorY = view.transform.position.y;
            bool overlaps = false;
            foreach (Cloud cloud in clouds)
            {
                if (!cloud.Bounds.Overlaps(tag))
                {
                    continue;
                }
                // UIHierarchySorter draws higher floor lines first; ties stay behind the cloud.
                if (cloud.FloorY <= floorY)
                {
                    return false;
                }
                overlaps = true;
            }
            return overlaps;
        }

        private static Sprite GetArt(MapMarker marker)
        {
            if (marker.Icon != null)
            {
                Sprite portrait = MarkerArt.ForPortrait(marker.Icon, marker.IconIsPortrait);
                if (portrait != null)
                {
                    return portrait;
                }
            }
            return MarkerArt.ForGlyph(marker.Glyph, marker.Color);
        }

        /// <summary>
        /// Converts the "screen pixels per art pixel" setting to map units, so marker pixels stay
        /// square and the same size as the map's own pixel art regardless of resolution / UI scale.
        /// </summary>
        private float GetUnitsPerArtPixel()
        {
            float screenPixelsPerUnit = 1f;
            Canvas canvas = container.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Canvas root = canvas.rootCanvas;
                float rootScale = Mathf.Abs(root.transform.lossyScale.x);
                screenPixelsPerUnit = root.scaleFactor * (rootScale > 0f ? Mathf.Abs(container.lossyScale.x) / rootScale : 1f);
            }
            if (screenPixelsPerUnit < 0.0001f)
            {
                screenPixelsPerUnit = 1f;
            }
            return Plugin.Settings.PixelSize.Value / screenPixelsPerUnit;
        }

        private void ReleaseUnseen()
        {
            var stale = new List<string>();
            foreach (KeyValuePair<string, MarkerView> pair in activeViews)
            {
                if (!seenKeys.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }
            foreach (string key in stale)
            {
                MarkerView view = activeViews[key];
                activeViews.Remove(key);
                if (view != null)
                {
                    if (view == gamepadSelected)
                    {
                        gamepadSelected = null;
                        virtualCursor?.DoAnimationTo(null);
                    }
                    view.gameObject.SetActive(false);
                    pool.Push(view);
                }
            }
        }

        private void HideAll()
        {
            seenKeys.Clear();
            ReleaseUnseen();
            foreach (LeaderView leader in leaders)
            {
                leader.gameObject.SetActive(false);
            }
        }

        /// <summary>The front layer sits directly above the cloud layer, which the game re-sends to the end on redraw.</summary>
        private void KeepFrontAboveClouds()
        {
            if (cloudsRoot == null || cloudsRoot.parent != frontContainer.parent)
            {
                frontContainer.SetAsLastSibling();
                return;
            }
            int cloudIndex = cloudsRoot.GetSiblingIndex();
            int frontIndex = frontContainer.GetSiblingIndex();
            if (frontIndex < cloudIndex)
            {
                frontContainer.SetSiblingIndex(cloudIndex);
            }
            else if (frontIndex > cloudIndex + 1)
            {
                frontContainer.SetSiblingIndex(cloudIndex + 1);
            }
        }

        private void KeepPlayerOnTop()
        {
            // Sit directly below the player icon without reordering the game's own layers
            // (fog-of-war clouds are placed after the player icon and should still cover markers).
            if (playerIcon == null || playerIcon.parent != container.parent)
            {
                return;
            }
            int playerIndex = playerIcon.GetSiblingIndex();
            int ownIndex = container.GetSiblingIndex();
            if (ownIndex > playerIndex)
            {
                container.SetSiblingIndex(playerIndex);
            }
            else if (ownIndex < playerIndex - 1)
            {
                container.SetSiblingIndex(playerIndex - 1);
            }
        }
    }
}
