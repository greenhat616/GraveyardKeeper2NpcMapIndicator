using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using GK2.MapMarkers.Api;
using UnityEngine;

namespace GK2.MapMarkers.Providers
{
    /// <summary>
    /// Config-driven category: matches world objects by definition id / group / interaction type.
    /// Used for mines, fishing spots, teleport pillars, cave entrances, and any future category
    /// that can be described by rules instead of code.
    ///
    /// Rule syntax (separated by ',' or ';'):
    ///   id:exact_id        exact definition id
    ///   prefix:iron_ore    id starts with
    ///   contains:fishing   id contains
    ///   group:some_group   WGODef.wgoGroup equals
    ///   type:ZombieMine    WGODef.interactionType equals (enum name)
    ///   !&lt;rule&gt;            exclusion; any matching exclusion rejects the object
    /// A bare token without "kind:" is treated as "prefix:".
    /// </summary>
    internal sealed class WgoRuleProvider : IMapMarkerProvider
    {
        private readonly ConfigEntry<bool> enabled;
        private readonly ConfigEntry<string> rules;
        private readonly ConfigEntry<string> color;
        private readonly ConfigEntry<float> scale;
        private readonly ConfigEntry<MarkerLabelMode> labelMode;
        private readonly int sortOrder;

        private readonly Dictionary<string, bool> classification = new Dictionary<string, bool>();
        private List<WgoRule> include = new List<WgoRule>();
        private List<WgoRule> exclude = new List<WgoRule>();
        private Color parsedColor;

        public WgoRuleProvider(
            string id,
            string displayName,
            ConfigEntry<bool> enabled,
            ConfigEntry<string> rules,
            ConfigEntry<string> color,
            ConfigEntry<float> scale,
            ConfigEntry<MarkerLabelMode> labelMode,
            int sortOrder)
        {
            Id = id;
            DisplayName = displayName;
            this.enabled = enabled;
            this.rules = rules;
            this.color = color;
            this.scale = scale;
            this.labelMode = labelMode;
            this.sortOrder = sortOrder;

            rules.SettingChanged += (_, __) => Reparse();
            color.SettingChanged += (_, __) => Reparse();
            Reparse();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public bool IsEnabled => enabled.Value && include.Count > 0;
        public MarkerLabelMode LabelMode => labelMode.Value;

        public void Collect(MapMarkerContext context, List<MapMarker> output)
        {
            IReadOnlyList<WgoData> all = context.AllWgo;
            for (int i = 0; i < all.Count; i++)
            {
                WgoData wgo = all[i];
                WGODef def = wgo.Definition;
                if (def == null || !Matches(def))
                {
                    continue;
                }

                output.Add(new MapMarker
                {
                    CategoryId = Id,
                    Key = wgo.UniqueId?.Guid.ToString() ?? wgo.id + i,
                    WorldPosition = wgo.Position,
                    WorldZoneId = WgoUtil.GetWorldZoneId(wgo),
                    Color = parsedColor,
                    Scale = scale.Value,
                    Label = DisplayName + ": " + WgoUtil.Localize(def.id),
                    SortOrder = sortOrder
                });
            }
        }

        private bool Matches(WGODef def)
        {
            if (classification.TryGetValue(def.id, out bool cached))
            {
                return cached;
            }

            bool result = false;
            foreach (WgoRule rule in include)
            {
                if (rule.Matches(def))
                {
                    result = true;
                    break;
                }
            }
            if (result)
            {
                foreach (WgoRule rule in exclude)
                {
                    if (rule.Matches(def))
                    {
                        result = false;
                        break;
                    }
                }
            }
            classification[def.id] = result;
            return result;
        }

        private void Reparse()
        {
            classification.Clear();
            var newInclude = new List<WgoRule>();
            var newExclude = new List<WgoRule>();
            foreach (string token in WgoUtil.SplitList(rules.Value))
            {
                if (WgoRule.TryParse(token, out WgoRule rule, out bool negated))
                {
                    (negated ? newExclude : newInclude).Add(rule);
                }
                else
                {
                    Plugin.Log?.Warning($"[{Id}] Ignoring invalid rule '{token}'.");
                }
            }
            include = newInclude;
            exclude = newExclude;
            parsedColor = ColorUtility.TryParseHtmlString(color.Value, out Color c) ? c : Color.white;
            MapMarkersApi.RequestRefresh();
        }
    }

    internal readonly struct WgoRule
    {
        private enum Kind { Id, Prefix, Contains, Group, Type }

        private readonly Kind kind;
        private readonly string value;
        private readonly WGODef.InteractionType type;

        private WgoRule(Kind kind, string value, WGODef.InteractionType type)
        {
            this.kind = kind;
            this.value = value;
            this.type = type;
        }

        public bool Matches(WGODef def)
        {
            switch (kind)
            {
                case Kind.Id: return string.Equals(def.id, value, StringComparison.OrdinalIgnoreCase);
                case Kind.Prefix: return def.id.StartsWith(value, StringComparison.OrdinalIgnoreCase);
                case Kind.Contains: return def.id.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
                case Kind.Group: return string.Equals(def.wgoGroup, value, StringComparison.OrdinalIgnoreCase);
                case Kind.Type: return def.interactionType == type;
                default: return false;
            }
        }

        public static bool TryParse(string token, out WgoRule rule, out bool negated)
        {
            rule = default;
            negated = token.StartsWith("!", StringComparison.Ordinal);
            if (negated)
            {
                token = token.Substring(1).Trim();
            }

            string kindText = "prefix";
            string value = token;
            int colon = token.IndexOf(':');
            if (colon >= 0)
            {
                kindText = token.Substring(0, colon).Trim().ToLowerInvariant();
                value = token.Substring(colon + 1).Trim();
            }
            if (value.Length == 0)
            {
                return false;
            }

            switch (kindText)
            {
                case "id": rule = new WgoRule(Kind.Id, value, default); return true;
                case "prefix": rule = new WgoRule(Kind.Prefix, value, default); return true;
                case "contains": rule = new WgoRule(Kind.Contains, value, default); return true;
                case "group": rule = new WgoRule(Kind.Group, value, default); return true;
                case "type":
                    if (Enum.TryParse(value, true, out WGODef.InteractionType t))
                    {
                        rule = new WgoRule(Kind.Type, value, t);
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }
    }
}
