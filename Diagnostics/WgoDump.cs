using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using GK2.MapMarkers.Providers;

namespace GK2.MapMarkers.Diagnostics
{
    /// <summary>
    /// Writes a table of all world-object definitions present in the current save, so rule-based
    /// categories (mines, fishing spots, ...) can be configured against real ids.
    /// </summary>
    internal static class WgoDump
    {
        public static void Write()
        {
            var all = new List<WgoData>();
            if (!WgoUtil.TryCollectAll(all))
            {
                Plugin.Log?.Warning("Dump skipped: no save is loaded.");
                return;
            }

            var rows = all
                .Where(w => w.Definition != null)
                .GroupBy(w => w.id)
                .Select(g =>
                {
                    WGODef def = g.First().Definition;
                    WgoData sample = g.First();
                    return new
                    {
                        Id = g.Key,
                        Count = g.Count(),
                        Type = def.interactionType.ToString(),
                        Group = def.wgoGroup ?? string.Empty,
                        Portrait = string.IsNullOrEmpty(def.portrait) ? string.Empty : "portrait",
                        Name = WgoUtil.Localize(def.id),
                        Sample = $"{sample.Position.x:0.#},{sample.Position.y:0.#},{sample.Position.z:0.#} scene={sample.WorldId} zone={WgoUtil.GetWorldZoneId(sample)}"
                    };
                })
                .OrderBy(r => r.Id, StringComparer.Ordinal)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"# Map Markers WGO dump {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {rows.Count} definitions, {all.Count} objects");
            sb.AppendLine("# id\tcount\tinteraction_type\twgo_group\tportrait\tlocalized_name\tsample_position");
            foreach (var r in rows)
            {
                sb.Append(r.Id).Append('\t').Append(r.Count).Append('\t').Append(r.Type).Append('\t')
                  .Append(r.Group).Append('\t').Append(r.Portrait).Append('\t').Append(r.Name).Append('\t')
                  .AppendLine(r.Sample);
            }

            string path = Path.Combine(Paths.BepInExRootPath, "MapMarkers_wgo_dump.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Plugin.Log?.Info($"Wrote {rows.Count} WGO definitions to {path}");
        }
    }
}
