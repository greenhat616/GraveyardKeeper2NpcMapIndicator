using System;
using System.Collections.Generic;
using LazyBearTechnology;
using UnityEngine;

namespace GK2.MapMarkers.Providers
{
    internal static class WgoUtil
    {
        private static readonly char[] ListSeparators = { ',', ';', '\n' };

        public static IEnumerable<string> SplitList(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }
            foreach (string part in text.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
        }

        public static string GetWorldZoneId(WgoData wgo)
        {
            try
            {
                return wgo.WorldZoneData?.id ?? FindZoneByPosition(wgo)?.id;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Some interior NPCs (e.g. Herbert, head of the guards, in the barracks) carry no world zone of their own.
        /// Fall back to the zone of their scene nearest to them (the game only assigns zones whose rectangle contains
        /// the object, so they may stand just outside it); among containing zones the smallest wins.
        /// </summary>
        private static WorldZoneData FindZoneByPosition(WgoData wgo)
        {
            const float MaxDistance = 8f;
            GameSceneData scene = MainGame.Instance?.GameSave?.WorldData?.GetGameSceneDataById(wgo.WorldId);
            if (scene?.worldZones == null)
            {
                return null;
            }
            var point = new Vector2(wgo.Position.x, wgo.Position.z);
            WorldZoneData best = null;
            float bestDistance = MaxDistance, bestArea = float.MaxValue;
            foreach (WorldZoneData zone in scene.worldZones)
            {
                if (zone == null)
                {
                    continue;
                }
                Rect rect = zone.wholeZoneRect;
                float dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
                float dz = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                float area = rect.width * rect.height;
                if (distance < bestDistance || (distance == bestDistance && area < bestArea))
                {
                    best = zone;
                    bestDistance = distance;
                    bestArea = area;
                }
            }
            return best;
        }

        public static string Localize(string id)
        {
            try
            {
                string text = LLBase.L(id);
                return string.IsNullOrEmpty(text) || text == id ? Prettify(id) : text;
            }
            catch (Exception)
            {
                return Prettify(id);
            }
        }

        /// <summary>"lake_village_forest_1" -> "Lake village forest 1" for ids without a localization entry.</summary>
        public static string Prettify(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }
            string text = id.Replace('_', ' ').Trim();
            return text.Length == 0 ? id : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>Collects every non-hidden WGO of the loaded save. Returns false outside gameplay.</summary>
        public static bool TryCollectAll(List<WgoData> buffer)
        {
            buffer.Clear();
            WorldData world = MainGame.Instance != null ? MainGame.Instance.GameSave?.worldData : null;
            if (world == null)
            {
                return false;
            }
            foreach (GameSceneData scene in world.gameSceneDataList)
            {
                if (scene?.wgoDataList == null)
                {
                    continue;
                }
                foreach (WgoData wgo in scene.wgoDataList)
                {
                    if (wgo != null && !wgo.IsHidden)
                    {
                        buffer.Add(wgo);
                    }
                }
            }
            return true;
        }
    }
}
