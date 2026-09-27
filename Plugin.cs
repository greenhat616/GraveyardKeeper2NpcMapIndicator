using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using GK2.Framework;
using GK2.MapMarkers.Api;
using GK2.MapMarkers.Diagnostics;
using GK2.MapMarkers.Map;
using GK2.MapMarkers.Providers;
using HarmonyLib;
using UnityEngine;

namespace GK2.MapMarkers
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(FrameworkPlugin.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "a632079.gk2.mapmarkers";
        public const string PluginName = "Map Markers";
        public const string PluginVersion = "0.2.0";

        internal static Gk2ModLogger Log { get; set; }
        internal static MapMarkersSettings Settings { get; set; }

        /// <summary>True between the framework's OnEnable and OnDisable.</summary>
        internal static bool IsActive { get; set; }

        private void Awake()
        {
            FrameworkApi.RegisterMod(new MapMarkersMod(), Config);
        }

        private void Update()
        {
            if (IsActive && Settings != null && Settings.DumpKey.Value.IsDown())
            {
                WgoDump.Write();
            }
        }
    }

    internal sealed class MapMarkersSettings
    {
        public ConfigEntry<float> RefreshInterval;
        public ConfigEntry<int> PixelSize;
        public ConfigEntry<float> PaperOpacity;
        public ConfigEntry<bool> SpreadOverlapping;
        public ConfigEntry<KeyboardShortcut> DumpKey;
    }

    internal sealed class MapMarkersMod : Gk2ModBase
    {
        private readonly Gk2ModMetadata metadata = new Gk2ModMetadata(
            Plugin.PluginGuid,
            Plugin.PluginName,
            "a632079",
            Plugin.PluginVersion,
            "Shows NPCs on the world map (live positions, portraits, names). Extensible marker categories: mines, fishing spots, teleport pillars, cave entrances.",
            supportsRuntimeToggle: true,
            requiresKnownBuild: false);

        private readonly IReadOnlyList<Gk2ModDependency> dependencies = new[]
        {
            new Gk2ModDependency(FrameworkPlugin.PluginGuid, "0.1.0", "0.2.0")
        };

        private readonly List<IMapMarkerProvider> builtInProviders = new List<IMapMarkerProvider>();
        private Harmony harmony;

        public override Gk2ModMetadata Metadata => metadata;
        public override IReadOnlyList<Gk2ModDependency> Dependencies => dependencies;

        public override void OnRegister(Gk2ModContext context)
        {
            Plugin.Log = context.Log;
            Gk2Settings s = context.Settings;

            Plugin.Settings = new MapMarkersSettings
            {
                RefreshInterval = s.AddFloatSlider("General", "RefreshInterval", 0.5f, 0.1f, 5f,
                    "Refresh interval (s)", "How often markers update while the map is open.", step: 0.1f, order: 0),
                PixelSize = s.AddIntSlider("General", "PixelSize", 2, 1, 6,
                    "Pixel size", "Screen pixels per marker-art pixel. Higher = bigger markers.", step: 1, order: 1),
                PaperOpacity = s.AddFloatSlider("General", "PaperOpacity", 0.8f, 0.2f, 1f,
                    "Parchment opacity", "Opacity of the parchment tag behind icons.", step: 0.05f, order: 2),
                SpreadOverlapping = s.AddToggle("General", "SpreadOverlapping", true,
                    "Spread overlapping markers", "Lay out overlapping markers side by side with ink lines to their real position.", order: 3),
                DumpKey = s.AddKeybind("Advanced", "DumpWgoIds", new KeyboardShortcut(KeyCode.F9, KeyCode.LeftControl),
                    "Dump world object ids",
                    "Writes every world object id / group / interaction type to BepInEx/MapMarkers_wgo_dump.txt. Use it to write category rules.",
                    order: 0)
            };

            builtInProviders.Add(new NpcMarkerProvider(
                s.AddToggle("NPC", "Enabled", true, "Show NPCs", "Draw NPC markers on the map.", order: 0),
                s.AddEnum("NPC", "Filter", NpcFilterMode.NamedOnly, "Which NPCs",
                    "NamedOnly: characters with a portrait or reputation. AllNpc: every npc_* object (citizens, guards, ...).", order: 1),
                s.AddEnum("NPC", "Labels", MarkerLabelMode.Hover, "Name labels", "When to show NPC names.", order: 2),
                s.AddFloatSlider("NPC", "Scale", 1f, 0.5f, 2f, "Marker scale", "Size multiplier for NPC markers.", step: 0.25f, order: 3),
                s.AddToggle("NPC", "Portraits", true, "Show portraits", "Draw the NPC portrait inside the marker.", order: 4),
                s.AddToggle("NPC", "HeadOnly", true, "Head portraits", "Crop a head-and-shoulders bust from the full-figure portrait.", order: 5),
                s.AddText("NPC", "Excluded", "npc_template, npc_goddess_statue", "Excluded ids",
                    "Comma-separated NPC definition ids that are never shown.", order: 6)));

            builtInProviders.Add(new TownVendorProvider(
                s.AddToggle("Town vendors", "Enabled", true, "Show town vendors",
                    "Mark the vendor shops you built in town, with the vendor's portrait and type.", order: 0),
                s.AddEnum("Town vendors", "Labels", MarkerLabelMode.Hover, "Labels", "When to show the vendor type.", order: 1),
                s.AddFloatSlider("Town vendors", "Scale", 1f, 0.5f, 2f, "Marker scale", "Size multiplier for vendor markers.", step: 0.25f, order: 2),
                s.AddToggle("Town vendors", "HeadOnly", true, "Head portraits", "Crop a head-and-shoulders bust from the vendor portrait.", order: 3)));

            // Other categories are pure configuration: rules match world-object definitions.
            for (int i = 0; i < BuiltInCategories.All.Count; i++)
            {
                AddRuleCategory(s, BuiltInCategories.All[i], sortOrder: 10 + i);
            }

            Plugin.Settings.PaperOpacity.SettingChanged += (_, __) => ApplyArtSettings();
            Plugin.Settings.PixelSize.SettingChanged += (_, __) => MapMarkersApi.RequestRefresh();
            Plugin.Settings.SpreadOverlapping.SettingChanged += (_, __) => MapMarkersApi.RequestRefresh();
            ApplyArtSettings();

            s.AddReadOnly("Status", "Summary", "Active categories", "Enabled marker providers.", Describe, order: 0);
            Plugin.Log.Info("MAP_MARKERS_REGISTERED");
        }

        private void AddRuleCategory(Gk2Settings s, RuleCategory category, int sortOrder)
        {
            string section = "Category: " + category.Name;
            ConfigEntry<string> rules = s.AddText(section, "Rules", category.Rules, "Match rules",
                "id:, prefix:, contains:, group:, type:<InteractionType>; prefix '!' to exclude. Separate with commas.", order: 1);
            foreach (string legacy in category.LegacyRules)
            {
                if (string.Equals(rules.Value.Trim(), legacy, StringComparison.Ordinal))
                {
                    Plugin.Log.Info($"[{category.Id}] Upgrading unmodified rules to the new default.");
                    rules.Value = category.Rules;
                    break;
                }
            }

            builtInProviders.Add(new RuleCategoryProvider(
                category,
                s.AddToggle(section, "Enabled", category.EnabledByDefault, "Show " + category.Name.ToLowerInvariant(), category.Description, order: 0),
                rules,
                s.AddText(section, "Color", category.Color, "Color", "HTML color, e.g. #ff8800.", order: 2),
                s.AddEnum(section, "Icon", category.Glyph, "Icon", "Pixel-art icon drawn in the tag.", order: 5),
                s.AddFloatSlider(section, "Scale", 1f, 0.5f, 2f, "Marker scale", "Size multiplier for this category.", step: 0.25f, order: 3),
                s.AddEnum(section, "Labels", MarkerLabelMode.Hover, "Labels", "When to show labels.", order: 4),
                sortOrder: sortOrder));
        }

        private static void ApplyArtSettings()
        {
            MarkerArt.SetPaperOpacity(Plugin.Settings.PaperOpacity.Value);
            MapMarkersApi.RequestRefresh();
        }

        private string Describe()
        {
            var names = new List<string>();
            foreach (IMapMarkerProvider p in MapMarkersApi.Providers)
            {
                if (p.IsEnabled)
                {
                    names.Add(p.DisplayName);
                }
            }
            return names.Count == 0 ? "none" : string.Join(", ", names);
        }

        public override void OnEnable()
        {
            foreach (IMapMarkerProvider provider in builtInProviders)
            {
                MapMarkersApi.RegisterProvider(provider);
            }
            MapMarkersApi.RefreshRequested += MapOverlay.RequestRefreshAll;

            harmony = new Harmony(Plugin.PluginGuid);
            harmony.Patch(
                AccessTools.Method(typeof(MapPageWidget), nameof(MapPageWidget.Redraw)),
                postfix: new HarmonyMethod(typeof(MapPatches), nameof(MapPatches.RedrawPostfix)));
            Plugin.IsActive = true;
            Plugin.Log.Info("MAP_MARKERS_ENABLED");
        }

        public override void OnDisable()
        {
            Plugin.IsActive = false;
            harmony?.UnpatchSelf();
            harmony = null;
            MapMarkersApi.RefreshRequested -= MapOverlay.RequestRefreshAll;
            foreach (IMapMarkerProvider provider in builtInProviders)
            {
                MapMarkersApi.UnregisterProvider(provider.Id);
            }
            MapOverlay.DestroyAll();
            MarkerArt.Clear();
            Plugin.Log.Info("MAP_MARKERS_DISABLED");
        }

        public override void OnReturnedToMainMenu()
        {
            SpriteCache.Clear();
        }
    }

    internal static class MapPatches
    {
        public static void RedrawPostfix(MapPageWidget __instance)
        {
            try
            {
                MapOverlay.AttachAndRefresh(__instance);
            }
            catch (System.Exception e)
            {
                Plugin.Log?.Error($"Map overlay failed: {e}");
            }
        }
    }
}
