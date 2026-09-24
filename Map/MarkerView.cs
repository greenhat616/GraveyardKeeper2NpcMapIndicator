using GK2.MapMarkers.Api;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GK2.MapMarkers.Map
{
    /// <summary>One marker on the map: a pixel-art parchment tag (see <see cref="MarkerArt"/>) plus a label.</summary>
    internal sealed class MarkerView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RectTransform rect;
        private Image image;
        private TextMeshProUGUI label;
        private MarkerLabelMode labelMode;
        private int baseSiblingIndex;

        public static MarkerView Create(Transform parent, TMP_Text fontTemplate)
        {
            var go = new GameObject("GK2MapMarker", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var view = go.AddComponent<MarkerView>();
            view.rect = (RectTransform)go.transform;
            view.rect.SetParent(parent, false);
            view.rect.anchorMin = view.rect.anchorMax = new Vector2(0.5f, 0.5f);

            view.image = go.AddComponent<Image>();
            view.image.type = Image.Type.Simple;

            if (fontTemplate != null && fontTemplate.font != null)
            {
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.layer = go.layer;
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.SetParent(view.rect, false);
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 1f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.anchoredPosition = new Vector2(0f, 1f);
                labelRect.sizeDelta = new Vector2(220f, 20f);
                view.label = labelGo.AddComponent<TextMeshProUGUI>();
                view.label.font = fontTemplate.font;
                view.label.fontSharedMaterial = fontTemplate.fontSharedMaterial;
                view.label.fontSize = 14f;
                view.label.textWrappingMode = TextWrappingModes.NoWrap;
                view.label.alignment = TextAlignmentOptions.Bottom;
                // Ink text with a parchment halo stays readable on both light and dark map areas.
                view.label.color = new Color(0.29f, 0.2f, 0.13f, 1f);
                view.label.outlineWidth = 0.25f;
                view.label.outlineColor = new Color32(236, 214, 168, 230);
                view.label.raycastTarget = false;
            }

            return view;
        }

        /// <param name="unitsPerArtPixel">Map units per marker-art pixel (keeps pixels crisp and uniform).</param>
        public void Apply(MapMarker marker, Sprite art, MarkerLabelMode mode, Vector2 anchoredPosition, float unitsPerArtPixel)
        {
            labelMode = mode;

            image.sprite = art;
            Vector2 size = art.rect.size;
            rect.pivot = new Vector2(art.pivot.x / size.x, art.pivot.y / size.y);
            rect.sizeDelta = size * unitsPerArtPixel * Mathf.Max(0.25f, marker.Scale);
            rect.anchoredPosition = anchoredPosition;

            // Only intercept the pointer when hovering is needed, so map dragging and milestones stay usable.
            image.raycastTarget = mode == MarkerLabelMode.Hover;

            if (label != null)
            {
                label.text = marker.Label ?? string.Empty;
                label.gameObject.SetActive(mode == MarkerLabelMode.Always && !string.IsNullOrEmpty(marker.Label));
            }
            gameObject.SetActive(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (labelMode != MarkerLabelMode.Hover || label == null || string.IsNullOrEmpty(label.text))
            {
                return;
            }
            baseSiblingIndex = rect.GetSiblingIndex();
            rect.SetAsLastSibling();
            label.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (labelMode != MarkerLabelMode.Hover || label == null)
            {
                return;
            }
            label.gameObject.SetActive(false);
            if (baseSiblingIndex < rect.parent.childCount)
            {
                rect.SetSiblingIndex(baseSiblingIndex);
            }
        }

        private void OnDisable()
        {
            if (label != null && labelMode == MarkerLabelMode.Hover)
            {
                label.gameObject.SetActive(false);
            }
        }
    }
}
