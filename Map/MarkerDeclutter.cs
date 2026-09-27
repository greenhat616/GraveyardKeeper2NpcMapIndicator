using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2.MapMarkers.Map
{
    /// <summary>
    /// Separates overlapping tags. Markers of the same group whose tag rectangles overlap are clustered
    /// (transitively); each cluster is laid out as rows of tags above the cluster, and every tag gets a
    /// leader line back to its true position. Single markers stay exactly where they are.
    /// </summary>
    internal static class MarkerDeclutter
    {
        private const int MaxPerRow = 5;

        internal sealed class Item
        {
            /// <summary>True map position (where the tag's pointer tip belongs).</summary>
            public Vector2 Anchor;
            /// <summary>Tag size in map units; the tag extends upwards from its tip.</summary>
            public Vector2 Size;
            /// <summary>Items only cluster with items of the same group (e.g. the same category).</summary>
            public string Group;

            /// <summary>Output: where the tip is drawn.</summary>
            public Vector2 Tip;
            /// <summary>Output: whether a leader line from <see cref="Tip"/> to <see cref="Anchor"/> is needed.</summary>
            public bool Displaced;
        }

        /// <param name="gap">Spacing between tags in map units.</param>
        /// <param name="lift">How far above the highest anchor a cluster's first row sits.</param>
        public static void Resolve(List<Item> items, float gap, float lift)
        {
            int n = items.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++)
            {
                parent[i] = i;
                items[i].Tip = items[i].Anchor;
                items[i].Displaced = false;
            }

            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }
                return i;
            }

            for (int i = 0; i < n; i++)
            {
                Item a = items[i];
                for (int j = i + 1; j < n; j++)
                {
                    Item b = items[j];
                    if (!string.Equals(a.Group, b.Group, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    // Tags span [tip.x - w/2, tip.x + w/2] x [tip.y, tip.y + h].
                    bool overlapX = Mathf.Abs(a.Anchor.x - b.Anchor.x) < (a.Size.x + b.Size.x) * 0.5f;
                    bool overlapY = a.Anchor.y < b.Anchor.y + b.Size.y && b.Anchor.y < a.Anchor.y + a.Size.y;
                    if (overlapX && overlapY)
                    {
                        parent[Find(i)] = Find(j);
                    }
                }
            }

            var clusters = new Dictionary<int, List<Item>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!clusters.TryGetValue(root, out List<Item> members))
                {
                    clusters[root] = members = new List<Item>();
                }
                members.Add(items[i]);
            }

            foreach (List<Item> members in clusters.Values)
            {
                if (members.Count > 1)
                {
                    LayoutCluster(members, gap, lift);
                }
            }
        }

        private static void LayoutCluster(List<Item> members, float gap, float lift)
        {
            // Left-to-right by true position keeps leader lines mostly uncrossed.
            members.Sort((a, b) => a.Anchor.x.CompareTo(b.Anchor.x));

            float centerX = 0f, topAnchor = float.MinValue, rowHeight = 0f;
            foreach (Item item in members)
            {
                centerX += item.Anchor.x;
                topAnchor = Mathf.Max(topAnchor, item.Anchor.y);
                rowHeight = Mathf.Max(rowHeight, item.Size.y);
            }
            centerX /= members.Count;

            int rows = Mathf.CeilToInt(members.Count / (float)MaxPerRow);
            int perRow = Mathf.CeilToInt(members.Count / (float)rows);
            float baseY = topAnchor + lift;

            for (int row = 0; row < rows; row++)
            {
                int start = row * perRow;
                int count = Mathf.Min(perRow, members.Count - start);
                float width = gap * (count - 1);
                for (int k = 0; k < count; k++)
                {
                    width += members[start + k].Size.x;
                }

                float x = centerX - width * 0.5f;
                float y = baseY + row * (rowHeight + gap);
                for (int k = 0; k < count; k++)
                {
                    Item item = members[start + k];
                    item.Tip = new Vector2(x + item.Size.x * 0.5f, y);
                    item.Displaced = true;
                    x += item.Size.x + gap;
                }
            }
        }
    }
}
