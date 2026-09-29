using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using GK2.MapMarkers.Api;
using LazyBearTechnology;
using UnityEngine;

namespace GK2.MapMarkers.Providers
{
    internal enum NpcFilterMode
    {
        /// <summary>Only NPCs that have a portrait or reputation (story / named characters).</summary>
        NamedOnly,
        /// <summary>Every WGO whose id starts with "npc_" (includes citizens, guards, ...).</summary>
        AllNpc
    }

    internal sealed class NpcMarkerProvider : IMapMarkerProvider
    {
        private const string NpcPrefix = "npc_";
        // Rank-and-file soldiers: village/forest/bonfire/city guards and the citizen patrols all have "_guard" in their
        // id; the barracks mercenaries (npc_town_barracks_mercenary_*) have "_mercenary".
        private static readonly string[] GuardMarkers = { "_guard", "_mercenary" };
        private const string GuardCaptainId = "npc_head_of_the_guards";

        private readonly ConfigEntry<bool> enabled;
        private readonly ConfigEntry<NpcFilterMode> filter;
        private readonly ConfigEntry<MarkerLabelMode> labelMode;
        private readonly ConfigEntry<float> mainScale;
        private readonly ConfigEntry<float> minorScale;
        private readonly ConfigEntry<bool> showPortraits;
        private readonly ConfigEntry<bool> headOnly;
        private readonly ConfigEntry<bool> showGuards;
        private readonly ConfigEntry<string> excluded;

        // def id -> include? Cleared whenever a filter setting changes.
        private readonly Dictionary<string, bool> classification = new Dictionary<string, bool>();
        private HashSet<string> excludedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Color NpcColor = new Color(0.55f, 0.36f, 0.2f, 1f);

        public NpcMarkerProvider(
            ConfigEntry<bool> enabled,
            ConfigEntry<NpcFilterMode> filter,
            ConfigEntry<MarkerLabelMode> labelMode,
            ConfigEntry<float> mainScale,
            ConfigEntry<float> minorScale,
            ConfigEntry<bool> showPortraits,
            ConfigEntry<bool> headOnly,
            ConfigEntry<bool> showGuards,
            ConfigEntry<string> excluded)
        {
            this.enabled = enabled;
            this.filter = filter;
            this.labelMode = labelMode;
            this.mainScale = mainScale;
            this.minorScale = minorScale;
            this.showPortraits = showPortraits;
            this.headOnly = headOnly;
            this.showGuards = showGuards;
            this.excluded = excluded;

            filter.SettingChanged += (_, __) => Invalidate();
            showGuards.SettingChanged += (_, __) => Invalidate();
            excluded.SettingChanged += (_, __) => Invalidate();
            Invalidate();
        }

        public string Id => "npc";
        public string DisplayName => "NPC";
        public bool IsEnabled => enabled.Value;
        public MarkerLabelMode LabelMode => labelMode.Value;

        public void Collect(MapMarkerContext context, List<MapMarker> output)
        {
            IReadOnlyList<WgoData> all = context.AllWgo;
            for (int i = 0; i < all.Count; i++)
            {
                WgoData wgo = all[i];
                WGODef def = wgo.Definition;
                if (def == null || !IsNpc(def))
                {
                    continue;
                }

                Sprite portrait = null;
                if (showPortraits.Value && !string.IsNullOrEmpty(def.portrait))
                {
                    portrait = SpriteCache.GetPortrait(def);
                }

                output.Add(new MapMarker
                {
                    CategoryId = Id,
                    Key = wgo.UniqueId?.Guid.ToString() ?? wgo.id + i,
                    WorldPosition = wgo.Position,
                    WorldZoneId = WgoUtil.GetWorldZoneId(wgo),
                    Icon = portrait,
                    IconIsPortrait = portrait != null && headOnly.Value,
                    Color = NpcColor,
                    // Characters with a reputation bar are the story cast; everyone else is background.
                    Scale = string.IsNullOrEmpty(def.repResName) ? minorScale.Value : mainScale.Value,
                    Label = WgoUtil.Localize(def.id),
                    SortOrder = portrait != null ? 20 : 10
                });
            }
        }

        private bool IsNpc(WGODef def)
        {
            if (classification.TryGetValue(def.id, out bool cached))
            {
                return cached;
            }

            bool result = def.id.StartsWith(NpcPrefix, StringComparison.OrdinalIgnoreCase)
                && !excludedIds.Contains(def.id)
                && (showGuards.Value || !IsGuard(def.id))
                && (filter.Value == NpcFilterMode.AllNpc
                    || !string.IsNullOrEmpty(def.portrait)
                    || !string.IsNullOrEmpty(def.repResName));
            classification[def.id] = result;
            return result;
        }

        /// <summary>A common guard or mercenary; the head of the guards (Herbert) is a named character and never counts.</summary>
        private static bool IsGuard(string id)
        {
            if (string.Equals(id, GuardCaptainId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            foreach (string marker in GuardMarkers)
            {
                if (id.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void Invalidate()
        {
            classification.Clear();
            excludedIds = new HashSet<string>(
                WgoUtil.SplitList(excluded.Value),
                StringComparer.OrdinalIgnoreCase);
            MapMarkersApi.RequestRefresh();
        }
    }

    internal static class SpriteCache
    {
        private static readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();

        public static Sprite GetPortrait(WGODef def)
        {
            if (portraits.TryGetValue(def.portrait, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            try
            {
                sprite = EasySpritesCollection.Instance.HasSprite(def.portrait) ? def.Portrait : null;
            }
            catch (Exception)
            {
                sprite = null;
            }
            portraits[def.portrait] = sprite;
            return sprite;
        }

        public static void Clear()
        {
            portraits.Clear();
        }
    }
}
