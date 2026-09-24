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
                return string.IsNullOrEmpty(text) ? id : text;
            }
            catch (Exception)
            {
                return id;
            }
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
