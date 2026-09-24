using System;
using System.Collections.Generic;
using GK2.MapMarkers.Api;

namespace GK2.MapMarkers.Providers
{
    /// <summary>Where a rule category takes its candidates from.</summary>
    internal enum RuleSource
    {
        /// <summary>World objects (WgoData); rules match the definition id / group / interaction type.</summary>
        Wgo,
        /// <summary>Scene transition GD points (doors, cave passages); rules match the point id.</summary>
        TransitPoint
    }

    /// <summary>Default definition of a config-driven marker category. Players can edit every value in the Mods menu.</summary>
    internal sealed class RuleCategory
    {
        public RuleCategory(string id, string name, string description, bool enabledByDefault, MarkerGlyph glyph, string color, string rules,
            RuleSource source = RuleSource.Wgo, Action<WgoData, MapMarker> decorate = null)
        {
            Id = id;
            Name = name;
            Description = description;
            EnabledByDefault = enabledByDefault;
            Glyph = glyph;
            Color = color;
            Rules = rules;
            Source = source;
            Decorate = decorate;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public bool EnabledByDefault { get; }
        public MarkerGlyph Glyph { get; }
        public string Color { get; }
        public string Rules { get; }
        public RuleSource Source { get; }

        /// <summary>Optional per-object tweak (label suffix, fading) applied after a WGO matched.</summary>
        public Action<WgoData, MapMarker> Decorate { get; }
    }

    /// <summary>
    /// Built-in categories. Ids were verified against the game's balance data (build 25467846);
    /// deliberately excluded because they are far too numerous to be useful: trees (tree_chop_*),
    /// bushes (bush_chop_*), grass and decor props.
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
            new RuleCategory("mine", "Mining spots", "Ore veins and stone, marble, clay and sand sources.",
                true, MarkerGlyph.Pickaxe, "#9aa3ad",
                "prefix:iron_ore, prefix:copper_vein, prefix:marble_source, prefix:marble_player_source, prefix:stone_player_source, "
                + "prefix:clay_spot, prefix:sand_pit, !contains:container, !contains:conveyor"),
            new RuleCategory("fishing", "Fishing spots", "Water bodies you can fish in; shows the fish left and fades when empty.",
                true, MarkerGlyph.Fish, "#4fb3e8",
                "type:Reservoir, !prefix:test_",
                decorate: FishingStatus.Decorate),
            new RuleCategory("cave", "Caves & descents", "Descent ladders, blocked caves and mine blockages.",
                true, MarkerGlyph.Ladder, "#c9853a",
                "prefix:descent_ladder, prefix:quarry_cave, id:mine_forest_blockage, id:basement_blockage_mine"),
            new RuleCategory("passage", "Cave passages", "Scene transitions such as cave tops/bottoms and ruined-temple routes.",
                true, MarkerGlyph.Cave, "#8a6a4a",
                "prefix:tp_cave, contains:ruined_temple, !contains:_dev_, !prefix:test_",
                source: RuleSource.TransitPoint),
            new RuleCategory("boulder", "Boulders", "Large breakable stones. Can be numerous, so off by default.",
                false, MarkerGlyph.Gem, "#b8b0a0",
                "prefix:stone_crash"),
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
            marker.Label += left > 0 ? $" ({left})" : " (0)";
            marker.Faded = left == 0;
        }
    }
}
