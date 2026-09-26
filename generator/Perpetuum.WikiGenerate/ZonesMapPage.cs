namespace Perpetuum.WikiGenerate;

/// <summary>
/// The zone map page: every zone plotted on the server's x/y grid (zones.x/y),
/// colored by galaxy family, with the rift gate connections that are recorded in
/// the server database (strongholdexitconfig + riftconfigs + riftdestinations).
/// Rendered as inline SVG so it themes with the site and stays crisp at any size.
/// </summary>
public static class ZonesMapPage
{
    private sealed record Zone(string Name, int Type, double X, double Y);

    public static string Build(Db db)
    {
        var zones = db.Query("SELECT id, name, zonetype, x, y, enabled FROM zones ORDER BY id")
            .Select(r => new Zone(r.Str("name"), r.Int("zonetype"), r.Dbl("x"), r.Dbl("y")))
            .ToList();

        // The zones table can hold several rows with the same name (e.g. zone_TM has
        // three — the extra rows sit at 50000/51000 sentinel coordinates). The map
        // plots one node per zone name, keeping the first (lowest id) row.
        var byName = zones.GroupBy(z => z.Name).ToDictionary(g => g.Key, g => g.First());
        var family = byName.Values.Select(z => (z, F: Family(z.Name))).ToList();

        // Viewbox: fit all coordinates with padding.
        var minX = family.Min(f => f.z.X);
        var maxX = family.Max(f => f.z.X);
        var minY = family.Min(f => f.z.Y);
        var maxY = family.Max(f => f.z.Y);
        var w = Math.Max(1, maxX - minX);
        var h = Math.Max(1, maxY - minY);
        var padX = w * 0.06;
        var padY = h * 0.04;
        const double vbW = 900;
        var vbH = vbW * (h + 2 * padY) / (w + 2 * padX);
        double Px(double x) => (x - minX + padX) / (w + 2 * padX) * vbW;
        double Py(double y) => vbH - (y - minY + padY) / (h + 2 * padY) * vbH; // y grows upward

        // Known gate connections from the DB: stronghold/arena exits and their
        // destinations (grouped — a zone can have several exit configs to the same
        // destination).
        var links = db.Query("""
            SELECT sz.name AS src, dz.name AS dst, rc.name AS rift
            FROM strongholdexitconfig se
            JOIN zones sz ON sz.id = se.zoneid
            JOIN riftconfigs rc ON rc.id = se.riftConfigId
            JOIN riftdestinations rd ON rd.groupId = rc.destinationGroupId
            JOIN zones dz ON dz.id = rd.zoneId
            """)
            .Select(r => (Src: r.Str("src"), Dst: r.Str("dst"), Rift: r.Str("rift")))
            .GroupBy(l => (l.Src, l.Dst))
            .Select(g => g.First())
            .OrderBy(l => l.Src).ThenBy(l => l.Dst)
            .ToList();

        // Inter-zone teleport points: teleportdescriptions records every TP column
        // (sourcezone -> targetzone); most of the 365 rows are intra-zone, the
        // inter-zone ones (1-4 per zone pair) are the real travel network.
        var tps = db.Query("""
            SELECT zs.name AS src, zd.name AS dst, COUNT(*) AS tps
            FROM teleportdescriptions td
            JOIN zones zs ON zs.id = td.sourcezone
            JOIN zones zd ON zd.id = td.targetzone
            WHERE td.sourcezone <> td.targetzone AND td.active = 1
            GROUP BY zs.name, zd.name
            """)
            .Select(r => (Src: r.Str("src"), Dst: r.Str("dst"), Tps: r.Int("tps")))
            .OrderBy(l => l.Src).ThenBy(l => l.Dst)
            .ToList();

        var sb = new StringBuilder();
        sb.Append(Md.Header("Zone map", "Every zone plotted on the server's x/y grid, colored by galaxy, with the rift gate connections recorded in the server database.",
            "zones (x/y, zonetype), strongholdexitconfig + riftconfigs + riftdestinations (gate links)"));
        sb.Append("\n\n# Zone map\n\n");
        sb.Append("All zones of the server, plotted at their real grid coordinates. Colors group the " +
                  "galaxies (the starter area, the main galaxy, the beta galaxy and the gamma belt); " +
                  "grey nodes are the special zones (training, PvP arena, strongholds). The thin lines are " +
                  "the inter-zone teleport points (TP columns) recorded in the database — each zone has a " +
                  "few of them, and the count per pair is shown in the line tooltip; the dashed lines are " +
                  "the stronghold/PvP-arena exit gates. Hover a node for its coordinates; zones with a wiki " +
                  "page are clickable.\n\n");

        sb.Append(Svg(family, byName, links, tps, vbW, vbH, Px, Py));
        sb.Append("\n");

        sb.Append("## Galaxies & zones\n\n");
        // Family colors live in style.css (--map-* variables) so colorblind
        // modes can recolor them; the key only selects the CSS class.
        var fams = new (string Label, string Key)[]
        {
            ("New Virginia (TM)", "tm"),
            ("Attalica (ICS)", "ics"),
            ("Daoden (ASI)", "asi"),
            ("Gamma belt", "gamma"),
            ("Special zones", "special"),
        };
        var frows = fams.Select(f =>
        {
            var zs = family.Where(x => x.F == f.Key).ToList();
            var types = string.Join(", ", zs.Select(z => TypeLabel(z.z.Type)).Distinct().OrderBy(t => t));
            return new[]
            {
                $"<span class=\"mapdot mapfam-{f.Key}\" role=\"img\" aria-label=\"{f.Label} color swatch\"></span> {f.Label}",
                zs.Count.ToString(),
                types.Length > 0 ? types : "–",
                string.Join(", ", zs.Select(z => z.z.Name).OrderBy(n => n)),
            };
        }).ToArray();
        Md.WriteTable(sb, new[] { "Family", "Zones", "Types", "Zone names" }, frows);
        sb.Append("\n");

        sb.Append("## Teleport connections (from the server database)\n\n");
        sb.Append($"Inter-zone TP columns recorded in `teleportdescriptions` (active rows only; " +
                  $"{tps.Count} directed pairs, {string.Join(", ", tps.Select(l => l.Tps).Distinct().OrderBy(n => n).Select(n => n.ToString()))} TP point(s) per pair). " +
                  "Pairs where both directions exist are listed once per direction, as recorded.\n\n");
        Md.WriteTable(sb, new[] { "From", "To", "TP points" },
            tps.Select(l => new[] { l.Src, l.Dst, l.Tps.ToString() }).ToArray());
        sb.Append("\n");

        sb.Append("## Stronghold & arena exit gates\n\n");
        sb.Append("The dashed lines — exits recorded in `strongholdexitconfig` / `riftconfigs` / `riftdestinations`. " +
                  "The entry-side rifts (e.g. the gates *into* the strongholds) are placed inside zone terrain " +
                  "files, so only the exit direction is known from the database.\n\n");
        if (links.Count > 0)
        {
            Md.WriteTable(sb, new[] { "From", "To", "Rift config" },
                links.Select(l => new[] { l.Src, l.Dst, $"`{l.Rift}`" }).ToArray());
        }
        sb.Append("\n_The Daoden stronghold instance (\"Daoden z2\") also has a recorded exit that sends players " +
                  "to a weighted-random destination among zone_ASI_pve, zone_ICS, zone_ICS_pve, zone_TM and " +
                  "zone_TM_pve — it is not a permanent zone, so it has no node on the map._\n\n");
        sb.Append("[Zones overview](/zones/)\n");
        return sb.ToString();
    }

    // Case-insensitive: the main zones are uppercase (zone_TM) while the gate
    // zones are lowercase (zone_tm_g_1).
    private static string Family(string name) =>
        name.Contains("gamma", System.StringComparison.OrdinalIgnoreCase) ? "gamma"
        : name.Contains("tm", System.StringComparison.OrdinalIgnoreCase) ? "tm"
        : name.Contains("ics", System.StringComparison.OrdinalIgnoreCase) ? "ics"
        : name.Contains("asi", System.StringComparison.OrdinalIgnoreCase) ? "asi"
        : "special";

    private static string TypeLabel(int t) => t switch
    {
        1 => "PvE",
        2 => "PvP",
        3 => "Training",
        4 => "Stronghold",
        _ => "Undefined",
    };

    /// <summary>Wiki page link for the zones that have one, null otherwise.</summary>
    private static string? PageLink(string name) => name switch
    {
        "zone_TM" => "/zones/zone-tm/",
        "zone_ASI" => "/zones/zone-asi/",
        "zone_gamma_z106" => "/zones/zone-gamma-z106/",
        _ => null,
    };

    private static string Svg(
        List<(Zone z, string F)> family,
        Dictionary<string, Zone> byName,
        List<(string Src, string Dst, string Rift)> links,
        List<(string Src, string Dst, int Tps)> tps,
        double vbW, double vbH, Func<double, double> Px, Func<double, double> Py)
    {
        var sb = new StringBuilder();
        sb.Append($"<svg viewBox=\"0 0 {vbW:0} {vbH:0}\" role=\"img\" aria-label=\"Map of all game zones, colored by galaxy family\" class=\"zonemap\">\n");
        sb.Append("  <title>Map of all game zones, colored by galaxy family</title>\n");
        foreach (var l in tps)
        {
            if (!byName.TryGetValue(l.Src, out var s) || !byName.TryGetValue(l.Dst, out var d)) continue;
            sb.Append($"  <line x1=\"{Px(s.X):0.#}\" y1=\"{Py(s.Y):0.#}\" x2=\"{Px(d.X):0.#}\" y2=\"{Py(d.Y):0.#}\" class=\"zonemap-tp\">" +
                      $"<title>{l.Src} — {l.Dst} ({l.Tps} TP point{(l.Tps > 1 ? "s" : "")})</title></line>\n");
        }
        foreach (var l in links)
        {
            if (!byName.TryGetValue(l.Src, out var s) || !byName.TryGetValue(l.Dst, out var d)) continue;
            sb.Append($"  <line x1=\"{Px(s.X):0.#}\" y1=\"{Py(s.Y):0.#}\" x2=\"{Px(d.X):0.#}\" y2=\"{Py(d.Y):0.#}\" class=\"zonemap-link\">" +
                      $"<title>{l.Src} — {l.Dst} (exit gate `{l.Rift}`)</title></line>\n");
        }
        // Gamma nodes first so the main-zone labels draw on top.
        foreach (var (z, f) in family.OrderByDescending(x => x.F == "gamma").ThenBy(x => x.z.Name, StringComparer.Ordinal))
        {
            var shortName = z.Name.StartsWith("zone_", StringComparison.Ordinal) ? z.Name[5..] : z.Name;
            var r = f == "gamma" ? 3.5 : 5.5;
            var fs = f == "gamma" ? 8 : 9.5;
            var x1 = Px(z.X);
            var y1 = Py(z.Y);
            var node = $"<circle cx=\"{x1:0.#}\" cy=\"{y1:0.#}\" r=\"{r}\" class=\"mapfam-{f} zonemap-node-{f}\">" +
                       $"<title>{z.Name} — {TypeLabel(z.Type)} ({z.X:0} / {z.Y:0})</title></circle>" +
                       $"<text x=\"{x1 + r + 2:0.#}\" y=\"{y1 + fs / 3:0.#}\" font-size=\"{fs}\" class=\"zonemap-label\">{shortName}</text>";
            var link = PageLink(z.Name);
            sb.Append(link is null
                ? $"  <g>{node}</g>\n"
                : $"  <a href=\"{link}\"><g>{node}</g></a>\n");
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }
}
