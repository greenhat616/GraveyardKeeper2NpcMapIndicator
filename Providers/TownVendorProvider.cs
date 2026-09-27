using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using GK2.MapMarkers.Api;
using LazyBearTechnology;
using UnityEngine;

namespace GK2.MapMarkers.Providers
{
    /// <summary>
    /// Vendors the player has built in town. When a town building (e.g. Pharmacy_t1..t3) is finished, TownSystem
    /// spawns a tent WGO (t_b_tent_*) that carries the TownBuildingWgoComponent; its TownBuildingDef.vendorId
    /// points to a VendorDef whose icon is the vendor character (t_b_apothecary, ...) with its own portrait.
    /// </summary>
    internal sealed class TownVendorProvider : IMapMarkerProvider
    {
        private const string TentTagPrefix = "t_b_tent_";

        private static readonly Color VendorColor = new Color(0.89f, 0.76f, 0.35f, 1f);

        private readonly ConfigEntry<bool> enabled;
        private readonly ConfigEntry<MarkerLabelMode> labelMode;
        private readonly ConfigEntry<float> scale;
        private readonly ConfigEntry<bool> headOnly;

        // One building shares its component object between its place and its tent; keep one marker per building.
        private readonly Dictionary<TownBuildingWgoComponent, WgoData> buildings = new Dictionary<TownBuildingWgoComponent, WgoData>();

        public TownVendorProvider(ConfigEntry<bool> enabled, ConfigEntry<MarkerLabelMode> labelMode, ConfigEntry<float> scale, ConfigEntry<bool> headOnly)
        {
            this.enabled = enabled;
            this.labelMode = labelMode;
            this.scale = scale;
            this.headOnly = headOnly;
            headOnly.SettingChanged += (_, __) => MapMarkersApi.RequestRefresh();
        }

        public string Id => "vendor";
        public string DisplayName => "Town vendors";
        public bool IsEnabled => enabled.Value;
        public MarkerLabelMode LabelMode => labelMode.Value;

        public void Collect(MapMarkerContext context, List<MapMarker> output)
        {
            buildings.Clear();
            IReadOnlyList<WgoData> all = context.AllWgo;
            for (int i = 0; i < all.Count; i++)
            {
                WgoData wgo = all[i];
                TownBuildingWgoComponent building = wgo.TownBuildingWgoComponent;
                if (building == null || !building.IsActive)
                {
                    continue;
                }
                // Prefer the tent: it is the building actually standing in town.
                if (!buildings.TryGetValue(building, out WgoData current) || (!IsTent(current) && IsTent(wgo)))
                {
                    buildings[building] = wgo;
                }
            }

            foreach (KeyValuePair<TownBuildingWgoComponent, WgoData> pair in buildings)
            {
                TownBuildingDef def = GetDef(pair.Key);
                if (def == null || def.townBuildingType != TownBuildingType.Vendor || string.IsNullOrEmpty(def.vendorId))
                {
                    continue;
                }

                WgoData wgo = pair.Value;
                Sprite portrait = GetPortrait(def);
                output.Add(new MapMarker
                {
                    CategoryId = Id,
                    Key = wgo.UniqueId?.Guid.ToString() ?? def.id,
                    WorldPosition = wgo.Position,
                    WorldZoneId = WgoUtil.GetWorldZoneId(wgo),
                    Icon = portrait,
                    IconIsPortrait = portrait != null && headOnly.Value,
                    Glyph = MarkerGlyph.Coin,
                    Color = VendorColor,
                    Scale = scale.Value,
                    Label = GetVendorName(def),
                    SortOrder = 15
                });
            }
        }

        private static bool IsTent(WgoData wgo)
        {
            return wgo.CustomTag != null && wgo.CustomTag.StartsWith(TentTagPrefix, StringComparison.Ordinal);
        }

        private static TownBuildingDef GetDef(TownBuildingWgoComponent building)
        {
            try
            {
                return building.TownBuildingDef;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The building's own character first (the potter's vendor is a test vendor without an icon), then VendorDef.icon.</summary>
        private static Sprite GetPortrait(TownBuildingDef def)
        {
            foreach (string wgoId in new[] { RootCharacterId(def), GetVendorIconId(def.vendorId) })
            {
                if (string.IsNullOrEmpty(wgoId))
                {
                    continue;
                }
                WGODef character = GameBalance.Me.GetDataOrNull<WGODef>(wgoId);
                if (character != null && !string.IsNullOrEmpty(character.portrait))
                {
                    Sprite sprite = SpriteCache.GetPortrait(character);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                }
            }
            return null;
        }

        /// <summary>The vendor's type, e.g. "Apothecary": the first id in the chain that has a translation.</summary>
        private static string GetVendorName(TownBuildingDef def)
        {
            foreach (string key in new[] { RootCharacterId(def), def.vendorId, def.id })
            {
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }
                string text = LLBase.L(key);
                if (!string.IsNullOrEmpty(text) && text != key)
                {
                    return text;
                }
            }
            return WgoUtil.Prettify(def.vendorId);
        }

        private static string GetVendorIconId(string vendorId)
        {
            // VendorDef.icon is private; its public Icon property already resolves the portrait, but we need the
            // character WGO id to go through the shared portrait cache, so read the field.
            VendorDef vendor = GameBalance.Me.GetDataOrNull<VendorDef>(vendorId);
            return vendor != null ? HarmonyLib.Traverse.Create(vendor).Field("icon").GetValue<string>() : null;
        }

        /// <summary>
        /// Only tier 1 names the character; upgraded tiers (Pharmacy_t2/t3) leave characterId empty, so walk the
        /// lvl_up chain back to the first tier.
        /// </summary>
        private static string RootCharacterId(TownBuildingDef def)
        {
            var visited = new HashSet<string>();
            TownBuildingDef current = def;
            while (current != null && visited.Add(current.id))
            {
                if (!string.IsNullOrEmpty(current.characterId))
                {
                    return current.characterId;
                }
                current = GameBalance.Me.townBuildingDefs.Find(other => other.lvlUpId == current.id);
            }
            return null;
        }
    }
}
