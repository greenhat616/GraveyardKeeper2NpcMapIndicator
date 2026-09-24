using System;
using System.Collections.Generic;
using GK2.MapMarkers.Api;

namespace GK2.MapMarkers.Providers
{
    /// <summary>Default definition of a config-driven marker category. Players can edit every value in the Mods menu.</summary>
    internal sealed class RuleCategory
    {
        public RuleCategory(string id, string name, string description, bool enabledByDefault, MarkerGlyph glyph, string color, string rules,
            Action<WgoData, MapMarker> decorate = null, params string[] legacyRules)
        {
            Id = id;
            Name = name;
            Description = description;
            EnabledByDefault = enabledByDefault;
            Glyph = glyph;
            Color = color;
            Rules = rules;
            Decorate = decorate;
            LegacyRules = legacyRules ?? Array.Empty<string>();
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public bool EnabledByDefault { get; }
        public MarkerGlyph Glyph { get; }
        public string Color { get; }
        public string Rules { get; }

        /// <summary>Optional per-object tweak (label suffix, fading) applied after a WGO matched.</summary>
        public Action<WgoData, MapMarker> Decorate { get; }

        /// <summary>
        /// Defaults shipped by earlier versions. A saved value equal to one of these was never edited by the
        /// player, so it is upgraded to the current <see cref="Rules"/> instead of being kept forever.
        /// </summary>
        public IReadOnlyList<string> LegacyRules { get; }
    }

    /// <summary>
    /// Built-in categories. Ids were verified against the WGODef table extracted from GameBalance
    /// (build 25467846). Deliberately not offered because they are far too numerous: trees, bushes,
    /// stumps, grass, trash pots and other decor.
    /// Pitfalls: iron_ore_* are containers (the ore node is common_ores_*); group:stones also contains
    /// junk and barrels; four fishing reservoir ids contain a space ("fishing_ place_home"), and several
    /// type:Reservoir objects (lake_village_forest_1, sea_town_docks_1, ...) have no fish defined.
    /// </summary>
    internal static class BuiltInCategories
    {
        /// <summary>Order here is the section order in the Mods menu and the draw order on the map.</summary>
        public static readonly IReadOnlyList<RuleCategory> All = new[]
        {
            new RuleCategory("teleport", "Teleport pillars",
                "Teleport milestones. The vanilla map already shows them, so this is off by default.",
                false, MarkerGlyph.Pillar, "#b98cff",
                "type:TeleportMilestone"),
            new RuleCategory("portal", "Portals", "The portal and its builder site.",
                true, MarkerGlyph.Star, "#e2c35a",
                "prefix:portal_builder"),
            new RuleCategory("mine", "Ores & quarries", "Iron ore, copper vein, marble and stone quarries, sand and clay pits.",
                true, MarkerGlyph.Pickaxe, "#9aa3ad",
                "prefix:common_ores_, id:copper_vein, prefix:marble_source_, id:marble_player_source, id:stone_player_source, "
                + "prefix:sand_pit, prefix:clay_spot"),
            new RuleCategory("stones", "Stones", "Common mineable stones. Numerous, so off by default.",
                false, MarkerGlyph.Gem, "#b8b0a0",
                "prefix:common_stones_"),
            new RuleCategory("fishing", "Fishing spots", "Waters you can fish in; shows the fish left and fades when empty.",
                true, MarkerGlyph.Fish, "#4fb3e8",
                "prefix:fishing_",
                FishingStatus.Decorate,
                "contains:fishing_place, contains:fishing_spot", "type:Reservoir, !prefix:test_"),
            new RuleCategory("cave", "Caves & descents", "Cave passages, the mine, sewers, descent ladders and blocked caves.",
                true, MarkerGlyph.Cave, "#c9853a",
                "prefix:tp_cave_, prefix:descent_ladder, id:tp_RT_mine_enter, id:tp_RT_town_sewer_enter, "
                + "id:tp_RT_scout_sewer_01_enter, id:tp_RT_scout_sewer_02_enter, id:quarry_cave_blocked, id:mine_forest_blockage"),
            new RuleCategory("entrance", "Doors & basements", "Building entrances, basements and area transitions. Off by default.",
                false, MarkerGlyph.Ladder, "#8a6a4a",
                "contains:_enter, contains:_outside, prefix:tp_RT_to_, prefix:tp_VFA_to_, prefix:tp_ruined_temple_to_, "
                + "prefix:basement_blockage_, prefix:base_blockage_, "
                + "!contains:sewer, !id:tp_RT_mine_enter, !id:church_tribune_outside, !contains:dev_, !prefix:test_, !contains:darkness"),
        };
    }

    /// <summary>Reads remaining fish per reservoir the same way ReservoirInteractionHandler does.</summary>
    internal static class FishingStatus
    {
        public static void Decorate(WgoData wgo, MapMarker marker)
        {
            List<FishingDef> fish;
            try
            {
                fish = FishingDef.GetAllForReservoir(wgo.id);
            }
            catch (Exception)
            {
                return;
            }
            if (fish == null || fish.Count == 0)
            {
                return;
            }

            int left = 0;
            foreach (FishingDef def in fish)
            {
                left += Math.Max(0, wgo.GetGameResInt(def.fishId));
            }
            marker.Label += $" ({left})";
            marker.Faded = left == 0;
        }
    }
}
