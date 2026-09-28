namespace Perpetuum.WikiGenerate;

/// <summary>
/// The zone map page: every zone drawn as an island (a rounded rectangle sized
/// to the zone's real width/height) on the server's x/y grid (zones.x/y),
/// colored by galaxy family, with the teleport columns and rift gate
/// connections recorded in the server database. Rendered as inline SVG so it
/// themes with the site; static/map.js adds zoom (wheel) and pan (drag), and
/// every island links to a zone page.
/// </summary>
public static class ZonesMapPage
{
    private sealed record Zone(string Name, int Type, double X, double Y, int W, int H, bool Protected, bool Terraformable);

    private sealed record Group(string Id, string Label, string Blurb, List<Zone> Zones);

    public static string Build(Db db)
    {
        var zones = db.Query("SELECT id, name, zonetype, x, y, width, height, protected, terraformable, enabled FROM zones ORDER BY id")
            .Select(r => new Zone(r.Str("name"), r.Int("zonetype"), r.Dbl("x"), r.Dbl("y"),
                r.Int("width"), r.Int("height"), r.Bit("protected"), r.Bit("terraformable")))
            .ToList();

        // The zones table can hold several rows with the same name (e.g. zone_TM has
        // three — the extra rows sit at 50000/51000 sentinel coordinates). The map
        // plots one node per zone name, keeping the first (lowest id) row.
        var byName = zones.GroupBy(z => z.Name).ToDictionary(g => g.Key, g => g.First());
        var all = byName.Values.ToList();

        // Viewbox from the real coordinates only: the 50000/51000 sentinel rows must
        // not stretch the canvas (they would leave the real map as a sliver in a
        // corner). Every plotted zone's primary row has a real coordinate, so
        // excluding the sentinels never drops a node.
        var real = all.Where(z => z.X < 49000 && z.Y < 49000).ToList();
        var minX = real.Min(z => z.X);
        var maxX = real.Max(z => z.X);
        var minY = real.Min(z => z.Y);
        var maxY = real.Max(z => z.Y);
        var w = Math.Max(1, maxX - minX);
        var h = Math.Max(1, maxY - minY);
        const double vbW = 1000;
        const double padFrac = 0.10; // extra room: islands have extent and labels sit outside
        var vbH = vbW * (h + 2 * padFrac * h) / (w + 2 * padFrac * w);
        double Px(double x) => (x - minX + padFrac * w) / (w + 2 * padFrac * w) * vbW;
        double Py(double y) => vbH - (y - minY + padFrac * h) / (h + 2 * padFrac * h) * vbH; // y grows upward

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

        var groups = Groups(all);

        var sb = new StringBuilder();
        sb.Append(Md.Header("World", "Every zone drawn to scale on the server grid, colored by galaxy, with the teleport and gate connections from the database.",
            "zones (x/y, width, protected, terraformable), teleportdescriptions, strongholdexitconfig + riftconfigs + riftdestinations"));
        sb.Append("\n\n# World\n\n");
        sb.Append("All zones of the server, drawn at their real grid coordinates — each island's size is its " +
                  "real width in tiles (the legend below the map shows the sizes). Colors group the galaxies; " +
                  "grey is the special zones (training, PvP arena, strongholds). The thin lines are the " +
                  "inter-zone teleport columns recorded in the database (the count per pair is in the line " +
                  "tooltip); the dashed lines are the stronghold/PvP-arena exit gates. **Scroll over the map to zoom** (no key needed), **drag " +
                  "to pan**, and click an island to open its page (islands without a dedicated page go to the " +
                  "[zone index](/zones/zone-index/)). Hover an island for its name, protection level and " +
                  "coordinates.\n\n");

        sb.Append("<div class=\"zonemap-wrap\">\n");
        sb.Append("<button type=\"button\" class=\"zonemap-reset\" title=\"Reset the zoom\">⟲</button>\n");
        sb.Append(Svg(all, byName, links, tps, vbW, vbH, Px, Py));
        sb.Append("</div>\n\n");
        sb.Append("<div class=\"zonemap-legend\">Island sizes (tiles): ");
        foreach (var (lab, px) in LegendSizes())
        {
            var style = $"width:{px * 0.5:0.#}px;min-width:6px";
            sb.Append($"<span class=\"legend-isle\" style=\"{style}\"></span> {lab};&ensp; ");
        }
        sb.Append("</div>\n\n");

        // Sub-category sections (also the side-menu anchors under "World").
        foreach (var g in groups)
        {
            sb.Append($"<a id=\"{g.Id}\"></a>\n\n## {g.Label}\n\n{g.Blurb}\n\n");
            var rows = g.Zones.Select(z => new[]
            {
                ZoneName(z.Name),
                TypeLabel(z.Type),
                $"{z.W}×{z.H}",
                Link(z.Name),
            }).ToArray();
            Md.WriteTable(sb, new[] { "Zone", "Type", "Size", "Page" }, rows);
            sb.Append("\n");
        }

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
        sb.Append("[Zones overview](/zones/) · [Zone index](/zones/zone-index/) · [Protection levels](/zones/protection/)\n");
        return sb.ToString();
    }

    /// <summary>Island size in viewbox units for a zone's real width.</summary>
    private static double Size(double w) => w switch
    {
        >= 1800 => 38,
        >= 800 => 24,
        >= 380 => 15,
        _ => 10,
    };

    private static IEnumerable<(string Lab, double Px)> LegendSizes()
    {
        yield return ("2048 (main islands)", 38);
        yield return ("1024 (training)", 24);
        yield return ("512 (stronghold)", 15);
        yield return ("256 (tc zones)", 10);
    }

    /// <summary>The menu sub-categories, in the order the side menu lists them.
    /// The gamma frontier belt is split into T0 (the tc transit zones) and T1–T4
    /// by zone number (the server stores no per-zone tier field; the split is a
    /// navigation grouping, noted on the page).</summary>
    private static List<Group> Groups(List<Zone> all)
    {
        bool Is(string n, params string[] parts) =>
            parts.Any(p => n.Contains(p, System.StringComparison.OrdinalIgnoreCase));
        Zone? ByName(string n) => all.FirstOrDefault(z => z.Name == n);
        int GammaTier(Zone z)
        {
            if (!z.Name.StartsWith("zone_gamma_z", StringComparison.Ordinal)) return -1;
            if (!int.TryParse(z.Name["zone_gamma_z".Length..], out var n)) return -1;
            return n is >= 106 and <= 140 ? (n - 106) / 9 + 1 : -1;
        }
        string GammaBlurb(int t) => "Frontier belt islands with the server-recorded tier " +
            $"**T{t}** (zones.note). Open PvP and terraformable — see [Protection levels](/zones/protection/). " +
            "Each island's page has its ore configuration and TP connections.";;
        return new List<Group>
        {
            new("training", "Training",
                "The virtual training island — where new characters start and learn the basics before entering the main galaxies.",
                new List<Zone> { ByName("zone_training")! }),
            new("starter-islands", "Starter islands",
                "The main protected islands and their second-wave PvE companions — the safe starter economy (see [Protection levels](/zones/protection/)).",
                all.Where(z => z.Name == "zone_TM" || z.Name == "zone_ICS" || z.Name == "zone_ASI"
                    || z.Name == "zone_TM_pve" || z.Name == "zone_ICS_pve" || z.Name == "zone_ASI_pve")
                    .OrderBy(z => z.Name, StringComparer.Ordinal).ToList()),
            new("beta", "Beta",
                "The open-PvP islands: the three main islands' PvP twins (the \"_real\" names), the eight gate islands of each galaxy, " +
                "and the special zones (the PvP arena and the strongholds — protected instances, listed here for completeness).",
                all.Where(z => z.Name != "zone_training" && !Is(z.Name, "gamma")
                    && (z.Protected == false || z.Type == 4))
                    .OrderBy(z => z.Name, StringComparer.Ordinal).ToList()),
            new("t0", "Gamma T0 — tc transit",
                "The six small (256-tile) transit zones of the frontier belt. No ore configuration — pure travel nodes between the main galaxies and the belt.",
                all.Where(z => z.Name.Contains("tc", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(z => z.Name, StringComparer.Ordinal).ToList()),
            new("t1", "Gamma T1", GammaBlurb(1),
                all.Where(z => GammaTier(z) == 1).OrderBy(z => z.Name, StringComparer.Ordinal).ToList()),
            new("t2", "Gamma T2", GammaBlurb(2),
                all.Where(z => GammaTier(z) == 2).OrderBy(z => z.Name, StringComparer.Ordinal).ToList()),
            new("t3", "Gamma T3", GammaBlurb(3),
                all.Where(z => GammaTier(z) == 3).OrderBy(z => z.Name, StringComparer.Ordinal).ToList()),
            new("t4", "Gamma T4", GammaBlurb(4),
                all.Where(z => GammaTier(z) == 4).OrderBy(z => z.Name, StringComparer.Ordinal).ToList()),
        };
    }

    private static string Svg(
        List<Zone> all,
        Dictionary<string, Zone> byName,
        List<(string Src, string Dst, string Rift)> links,
        List<(string Src, string Dst, int Tps)> tps,
        double vbW, double vbH, Func<double, double> Px, Func<double, double> Py)
    {
        var sb = new StringBuilder();
        sb.Append($"<svg viewBox=\"0 0 {vbW:0} {vbH:0}\" role=\"img\" aria-label=\"Map of all game zones, drawn to scale, colored by galaxy family\" class=\"zonemap\" xmlns=\"http://www.w3.org/2000/svg\">\n");
        sb.Append("  <title>Map of all game zones, drawn to scale, colored by galaxy family</title>\n");
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
        // Small islands first so the big-island labels draw on top.
        foreach (var z in all.OrderBy(z => Size(z.W)).ThenBy(z => z.Name, StringComparer.Ordinal))
        {
            var f = Family(z.Name);
            var size = Size(z.W);
            var x1 = Px(z.X);
            var y1 = Py(z.Y);
            var shortName = z.Name.StartsWith("zone_", StringComparison.Ordinal) ? z.Name[5..] : z.Name;
            var tip = $"{z.Name} — {ProtectionLabel(z)} · {TypeLabel(z.Type)} ({z.X:0} / {z.Y:0})";
            var shape = f == "special"
                ? $"<circle cx=\"{x1:0.#}\" cy=\"{y1:0.#}\" r=\"{size / 2}\" class=\"mapfam-{f} zonemap-node-{f}\"><title>{tip}</title></circle>"
                : $"<rect x=\"{x1 - size / 2:0.#}\" y=\"{y1 - size / 2:0.#}\" width=\"{size:0.#}\" height=\"{size:0.#}\" rx=\"{size * 0.3:0.#}\" class=\"mapfam-{f} zonemap-node-{f}\"><title>{tip}</title></rect>";
            var label = f == "gamma" ? 10.5 : 13;
            var text = $"<text x=\"{x1 + size / 2 + 3:0.#}\" y=\"{y1 + label / 3:0.#}\" font-size=\"{label}\" class=\"zonemap-label\">{shortName}</text>";
            var href = PageLink(z.Name);
            sb.Append($"  <a href=\"{href}\"><g>{shape}{text}</g></a>\n");
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    /// <summary>Case-insensitive: the main zones are uppercase (zone_TM) while the gate
    /// zones are lowercase (zone_tm_g_1).</summary>
    private static string Family(string name) =>
        name.Contains("gamma", System.StringComparison.OrdinalIgnoreCase) ? "gamma"
        : name.Contains("tm", System.StringComparison.OrdinalIgnoreCase) ? "tm"
        : name.Contains("ics", System.StringComparison.OrdinalIgnoreCase) ? "ics"
        : name.Contains("asi", System.StringComparison.OrdinalIgnoreCase) ? "asi"
        : "special";

    /// <summary>Protection level straight from the zones flags
    /// (ZoneConfiguration.IsAlpha/IsBeta/IsGamma): alpha = protected,
    /// gamma = terraformable, beta = the open rest.</summary>
    private static string ProtectionLabel(Zone z)
    {
        if (z.Protected) return "protected (alpha)";
        return z.Terraformable ? "open (gamma)" : "open (beta)";
    }

    private static string TypeLabel(int t) => t switch
    {
        1 => "PvE",
        2 => "PvP",
        3 => "Training",
        4 => "Stronghold",
        _ => "Undefined",
    };

    private static string ZoneName(string n)
    {
        if (Md.ClientStrings.TryGetValue(n, out var d) && d != n) return d;
        return n.StartsWith("zone_", StringComparison.Ordinal) ? n[5..] : n;
    }

    /// <summary>Page for a zone — every zone has its own page (the three worked
    /// examples are hand-written, the rest generated; same slug scheme).</summary>
    private static string PageLink(string name) =>
        "/zones/" + name.ToLowerInvariant().Replace("_", "-") + "/";

    private static string Link(string name)
    {
        var href = PageLink(name);
        return $"[{ZoneName(name)}]({href})";
    }
}
