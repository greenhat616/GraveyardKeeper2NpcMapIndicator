using System;
using System.Collections.Generic;
using LazyBearTechnology;

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
                return wgo.WorldZoneData?.id;
            }
            catch (Exception)
            {
                return null;
            }
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
