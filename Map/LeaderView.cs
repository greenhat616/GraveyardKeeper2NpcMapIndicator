using UnityEngine;
using UnityEngine.UI;

namespace GK2.MapMarkers.Map
{
    /// <summary>An ink leader line from a displaced tag's tip to the marker's true position, ending in a dot.</summary>
    internal sealed class LeaderView : MonoBehaviour
    {
        private static readonly Color InkColor = new Color32(74, 50, 32, 215);

        private RectTransform line;
        private RectTransform dot;

        public static LeaderView Create(Transform parent)
        {
            var go = new GameObject("GK2MapMarkerLeader", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var root = (RectTransform)go.transform;
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;

            var view = go.AddComponent<LeaderView>();
            view.line = CreatePart(root, "Line", new Vector2(0f, 0.5f));
            view.dot = CreatePart(root, "Dot", new Vector2(0.5f, 0.5f));
            return view;
        }

        /// <param name="pixel">Map units per marker-art pixel; line and dot keep the pixel-art scale.</param>
        public void Apply(Vector2 from, Vector2 to, float pixel, bool faded)
        {
            Vector2 delta = to - from;
            line.anchoredPosition = from;
            line.sizeDelta = new Vector2(delta.magnitude, pixel);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            dot.anchoredPosition = to;
            dot.sizeDelta = new Vector2(pixel * 3f, pixel * 3f);

            Color color = InkColor;
            if (faded)
            {
                color.a *= 0.45f;
            }
            line.GetComponent<Image>().color = color;
            dot.GetComponent<Image>().color = color;
            gameObject.SetActive(true);
        }

        private static RectTransform CreatePart(RectTransform parent, string name, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return rect;
        }
    }
}
