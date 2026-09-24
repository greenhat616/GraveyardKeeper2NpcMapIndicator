using System;
using System.Collections.Generic;
using System.Reflection;
using GK2.MapMarkers.Api;
using GK2.MapMarkers.Providers;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2.MapMarkers.Map
{
    /// <summary>
    /// Lives under MapPageWidget's map rect. Rebuilds markers when the map is redrawn and then
    /// periodically while it stays open, so moving NPCs are tracked live.
    /// </summary>
    internal sealed class MapOverlay : MonoBehaviour
    {
        private const string ObjectName = "GK2MapMarkersOverlay";
        private const float InteriorSpreadRadius = 12f; // in art pixels

        private static readonly FieldInfo MapRectField = AccessTools.Field(typeof(MapPageWidget), "mapRect");
        private static readonly FieldInfo PlayerIconField = AccessTools.Field(typeof(MapPageWidget), "playerIcon");
        private static readonly List<MapOverlay> Instances = new List<MapOverlay>();

        private readonly Dictionary<string, MarkerView> activeViews = new Dictionary<string, MarkerView>();
        private readonly Stack<MarkerView> pool = new Stack<MarkerView>();
        private readonly List<MapMarker> markers = new List<MapMarker>();
        private readonly List<WgoData> wgoBuffer = new List<WgoData>();
        private readonly Dictionary<string, MarkerLabelMode> labelModes = new Dictionary<string, MarkerLabelMode>();
        private readonly HashSet<string> seenKeys = new HashSet<string>();
        private readonly HashSet<string> failedProviders = new HashSet<string>();
        private readonly Dictionary<string, int> interiorTotals = new Dictionary<string, int>();
        private readonly Dictionary<string, int> interiorIndices = new Dictionary<string, int>();

        private MapPageWidget widget;
        private RectTransform mapRect;
        private RectTransform playerIcon;
        private RectTransform container;
        private TMP_Text fontTemplate;
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
            // Scenes load/unload GD points while playing; rebuild the transit list on every map open.
            WgoUtil.ClearSessionCaches();
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

            container = (RectTransform)transform;
            container.SetParent(mapRect, false);
            container.anchorMin = container.anchorMax = container.pivot = new Vector2(0.5f, 0.5f);
            container.anchoredPosition = Vector2.zero;

            foreach (TMP_Text text in owner.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font != null)
                {
                    fontTemplate = text;
                    break;
                }
            }
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
        }

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (dirty || timer >= Plugin.Settings.RefreshInterval.Value)
            {
                Refresh();
            }
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
            CollectMarkers();
            Layout(projection);
            KeepPlayerOnTop();
        }

        private void CollectMarkers()
        {
            markers.Clear();
            labelModes.Clear();
            var context = new MapMarkerContext(wgoBuffer, WgoUtil.GetTransitPoints);
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
            float unitsPerArtPixel = GetUnitsPerArtPixel();
            interiorTotals.Clear();
            interiorIndices.Clear();

            var projected = new List<(MapMarker marker, Vector2 pos, bool interior)>(markers.Count);
            foreach (MapMarker marker in markers)
            {
                if (!projection.TryProject(marker.WorldPosition, marker.WorldZoneId, out Vector2 pos, out bool interior))
                {
                    continue;
                }
                projected.Add((marker, pos, interior));
                if (interior)
                {
                    interiorTotals.TryGetValue(marker.WorldZoneId, out int n);
                    interiorTotals[marker.WorldZoneId] = n + 1;
                }
            }

            // Category order first, then top-to-bottom so lower tags overlap the ones above them.
            projected.Sort((a, b) =>
            {
                int bySort = a.marker.SortOrder.CompareTo(b.marker.SortOrder);
                return bySort != 0 ? bySort : b.pos.y.CompareTo(a.pos.y);
            });

            foreach (var (marker, basePos, interior) in projected)
            {
                Vector2 pos = basePos;
                if (interior)
                {
                    // Several NPCs in the same interior share one anchor: fan them out on a ring.
                    int total = interiorTotals[marker.WorldZoneId];
                    interiorIndices.TryGetValue(marker.WorldZoneId, out int index);
                    interiorIndices[marker.WorldZoneId] = index + 1;
                    if (total > 1)
                    {
                        float angle = index * Mathf.PI * 2f / total;
                        float radius = (InteriorSpreadRadius + 3f * Mathf.Max(0, total - 4)) * unitsPerArtPixel;
                        pos += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    }
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
                if (!activeViews.TryGetValue(key, out MarkerView view) || view == null)
                {
                    view = pool.Count > 0 ? pool.Pop() : MarkerView.Create(container, fontTemplate);
                    activeViews[key] = view;
                }
                labelModes.TryGetValue(marker.CategoryId, out MarkerLabelMode mode);
                view.Apply(marker, art, mode, pos, unitsPerArtPixel);
                view.transform.SetAsLastSibling(); // keeps SortOrder as draw order
            }

            ReleaseUnseen();
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
                    view.gameObject.SetActive(false);
                    pool.Push(view);
                }
            }
        }

        private void HideAll()
        {
            seenKeys.Clear();
            ReleaseUnseen();
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
