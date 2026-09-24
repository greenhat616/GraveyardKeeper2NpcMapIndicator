using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2.MapMarkers.Api
{
    /// <summary>How a marker's label is displayed on the map.</summary>
    public enum MarkerLabelMode
    {
        Hover,
        Always,
        Never
    }

    /// <summary>Built-in pixel-art glyphs for markers without an icon.</summary>
    public enum MarkerGlyph
    {
        Gem,
        Pickaxe,
        Fish,
        Pillar,
        Cave,
        Ladder,
        Coin,
        Cross,
        Star
    }

    /// <summary>
    /// One point drawn on the in-game map. Providers fill these; the overlay projects and renders them.
    /// </summary>
    public sealed class MapMarker
    {
        /// <summary>Provider/category id this marker belongs to (e.g. "npc", "mine").</summary>
        public string CategoryId;

        /// <summary>Stable key used to reuse marker views between refreshes (e.g. the WGO unique id).</summary>
        public string Key;

        /// <summary>World-space position (same space as WgoData.Position).</summary>
        public Vector3 WorldPosition;

        /// <summary>
        /// Game world-zone id (interior) the object is in. Used as a fallback anchor when the position
        /// is outside the overworld map, the same way the game places the player icon.
        /// </summary>
        public string WorldZoneId;

        /// <summary>
        /// Optional icon drawn inside the parchment tag. Null draws a gem glyph in <see cref="Color"/>.
        /// Blue-key outlines (pure blue, as used by game portraits) are recolored to ink.
        /// </summary>
        public Sprite Icon;

        /// <summary>True when <see cref="Icon"/> is a full-figure character portrait: a head-and-shoulders bust is cropped from it.</summary>
        public bool IconIsPortrait;

        /// <summary>Glyph drawn when <see cref="Icon"/> is null.</summary>
        public MarkerGlyph Glyph = MarkerGlyph.Gem;

        /// <summary>Glyph color used when <see cref="Icon"/> is null.</summary>
        public Color Color = Color.white;

        /// <summary>Size multiplier on top of the global pixel size (1 = default).</summary>
        public float Scale = 1f;

        /// <summary>Label text shown on hover / always.</summary>
        public string Label;

        /// <summary>Draw the marker dimmed (e.g. a depleted fishing spot).</summary>
        public bool Faded;

        /// <summary>Higher values are drawn on top.</summary>
        public int SortOrder;
    }

    /// <summary>
    /// A source of map markers. Implement this to add a new category (mines, fishing spots, ...),
    /// then call <see cref="MapMarkersApi.RegisterProvider"/>.
    /// </summary>
    public interface IMapMarkerProvider
    {
        /// <summary>Unique, stable id, lowercase (e.g. "npc").</summary>
        string Id { get; }

        /// <summary>Player-facing category name.</summary>
        string DisplayName { get; }

        /// <summary>Checked on every refresh; disabled providers are skipped.</summary>
        bool IsEnabled { get; }

        /// <summary>Label mode for markers produced by this provider.</summary>
        MarkerLabelMode LabelMode { get; }

        /// <summary>Append markers to <paramref name="output"/>. Called on the Unity main thread while the map is open.</summary>
        void Collect(MapMarkerContext context, List<MapMarker> output);
    }

    /// <summary>Per-refresh data shared by all providers.</summary>
    public sealed class MapMarkerContext
    {
        private readonly List<WgoData> allWgo;
        private readonly Func<IReadOnlyList<GDPointData>> transitPointsFactory;
        private IReadOnlyList<GDPointData> transitPoints;

        internal MapMarkerContext(List<WgoData> allWgo, Func<IReadOnlyList<GDPointData>> transitPointsFactory)
        {
            this.allWgo = allWgo;
            this.transitPointsFactory = transitPointsFactory;
        }

        /// <summary>All visible (non-hidden) world objects of the current save, collected once per refresh.</summary>
        public IReadOnlyList<WgoData> AllWgo => allWgo;

        /// <summary>Scene transition GD points (doors, cave passages). Built lazily and cached per session.</summary>
        public IReadOnlyList<GDPointData> TransitPoints => transitPoints ?? (transitPoints = transitPointsFactory?.Invoke() ?? Array.Empty<GDPointData>());
    }

    /// <summary>Public entry point for other mods / future extensions.</summary>
    public static class MapMarkersApi
    {
        private static readonly List<IMapMarkerProvider> providers = new List<IMapMarkerProvider>();

        /// <summary>Raised when providers change or a refresh is requested.</summary>
        internal static event Action RefreshRequested;

        public static IReadOnlyList<IMapMarkerProvider> Providers => providers;

        public static void RegisterProvider(IMapMarkerProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }
            if (providers.Exists(p => string.Equals(p.Id, provider.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Map marker provider '{provider.Id}' is already registered.");
            }
            providers.Add(provider);
            RequestRefresh();
        }

        public static bool UnregisterProvider(string id)
        {
            int removed = providers.RemoveAll(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                RequestRefresh();
            }
            return removed > 0;
        }

        /// <summary>Asks an open map to redraw markers on the next frame.</summary>
        public static void RequestRefresh()
        {
            RefreshRequested?.Invoke();
        }
    }
}
