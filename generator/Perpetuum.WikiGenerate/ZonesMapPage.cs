namespace Perpetuum.WikiGenerate;

/// <summary>
/// The zone map page: every zone drawn at its real grid coordinates
/// (zones.x/y) with the same design as its index card — the coastline-only
/// thumb.png (tools/gen_zone_teleport_maps.py) in the family color over a
/// dark plate, outlined by galaxy family — plus the teleport and rift gate
/// connections recorded in the server database: inter-zone TPs as dashed
/// lines gradient-colored from the source family color to the destination's,
/// exit gates as the classic dashed link lines. Rendered as inline SVG so it
/// themes with the site; map.js adds zoom (wheel) and pan (drag), and every
/// island links to a zone page. The sub-category sections below the map are
/// card grids (the same thumb.png) instead of stat tables.
/// </summary>
public static class ZonesMapPage
{
    private sealed record Zone(string Name, int Type, double X, double Y, int W, int H, bool Protected, bool Terraformable);

    /// <summary>Nullable double read (AVG over an all-NULL column is NULL).</summary>
    private static object? NulDbl(Dictionary<string, object?> r, string k)
        => r.TryGetValue(k, out var v) && v is not null ? Convert.ToDouble(v) : null;

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
        // The game's grid coordinates grow downward (the zone teleport maps
        // plot them directly on the terrain PNGs and everything aligns), so
        // the world map must NOT flip the axis: gamma sits at the top of the
        // map, the starter islands (TM/ASI/ICS + training) at the bottom.
        double Py(double y) => (y - minY + padFrac * h) / (h + 2 * padFrac * h) * vbH;

        // Known gate connections from the DB: stronghold/arena exits and their
        // destinations (grouped — a zone can have several exit configs to the same
        // destination). The gate positions (se.x/se.y) anchor the line to the
        // gate's real spot in the source zone.
        var links = db.Query("""
            SELECT sz.name AS src, dz.name AS dst, rc.name AS rift,
                   AVG(se.x) AS gx, AVG(se.y) AS gy
            FROM strongholdexitconfig se
            JOIN zones sz ON sz.id = se.zoneid
            JOIN riftconfigs rc ON rc.id = se.riftConfigId
            JOIN riftdestinations rd ON rd.groupId = rc.destinationGroupId
            JOIN zones dz ON dz.id = rd.zoneId
            GROUP BY sz.name, dz.name, rc.name
            """)
            .Select(r => (Src: r.Str("src"), Dst: r.Str("dst"), Rift: r.Str("rift"),
                Gx: (double?)NulDbl(r, "gx"), Gy: (double?)NulDbl(r, "gy")))
            .GroupBy(l => (l.Src, l.Dst))
            .Select(g => g.First())
            .OrderBy(l => l.Src).ThenBy(l => l.Dst)
            .ToList();

        // Inter-zone teleport points: teleportdescriptions records every TP column
        // (sourcezone -> targetzone); most of the 365 rows are intra-zone, the
        // inter-zone ones (1-4 per zone pair) are the real travel network. The
        // column positions (zoneentities x/y) and the landing spots
        // (targetx/targety) anchor each line to the REAL spots in both zones,
        // not their centers.
        var tps = db.Query("""
            SELECT zs.name AS src, zd.name AS dst, COUNT(*) AS tps,
                   AVG(ze.x) AS sx, AVG(ze.y) AS sy,
                   AVG(td.targetx) AS tx, AVG(td.targety) AS ty
            FROM teleportdescriptions td
            JOIN zones zs ON zs.id = td.sourcezone
            JOIN zones zd ON zd.id = td.targetzone
            JOIN zoneentities ze ON ze.eid = td.sourcecolumn
            WHERE td.sourcezone <> td.targetzone AND td.active = 1
            GROUP BY zs.name, zd.name
            """)
            .Select(r => (Src: r.Str("src"), Dst: r.Str("dst"), Tps: r.Int("tps"),
                Sx: (double?)NulDbl(r, "sx"), Sy: (double?)NulDbl(r, "sy"),
                Tx: (double?)NulDbl(r, "tx"), Ty: (double?)NulDbl(r, "ty")))
            .OrderBy(l => l.Src).ThenBy(l => l.Dst)
            .ToList();

        var sb = new StringBuilder();
        sb.Append(Md.Header("World", "Every zone drawn with its real terrain at its grid position, outlined by galaxy, with the teleport and gate connections from the database.",
            "zones (x/y, width, protected, terraformable), teleportdescriptions, zoneentities, strongholdexitconfig + riftconfigs + riftdestinations"));
        sb.Append("\n\n# World\n\n");
        sb.Append("Every zone with an inter-zone teleport connection, drawn at its real grid coordinates — " +
                  "each island outlined by galaxy family and sized to its real width in tiles (the legend below " +
                  "the map shows the sizes). The dashed lines are the teleport columns recorded in the database, " +
                  "colored from the source island's family color to the destination's, drawn over the islands " +
                  "and touching the real column/landing spots inside them (the count per pair is in the line " +
                  "tooltip). Zones without any teleport connection (strongholds, the PvP arena, dead-end gate " +
                  "islands) are left off the map. **Scroll over the map to zoom** (no key needed), **drag to pan**, " +
                  "and click an island to open its page (islands without a dedicated page go to the [zone index](/zones/zone-index/)). " +
                  "Hover an island for its name, protection level and coordinates.\n\n");

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
        // The per-family zone listings live on their own pages (see
        // FamilyPages) — the map page only links to them.
        sb.Append("Zone listings: [Alpha](/zones/alpha/) · [Beta](/zones/beta/) · [Gamma](/zones/gamma/) — ");
        sb.Append("[Zone index](/zones/zone-index/) · [Protection levels](/zones/protection/)\n");
        return sb.ToString();
    }

    /// <summary>The per-family zone-listing pages (/zones/alpha/, /zones/beta/,
    /// /zones/gamma/). Each family gets its own page with the card grid that
    /// used to be a section at the bottom of the world map page — the world
    /// map stays the map, the listings get a URL of their own (and the nav
    /// highlights the matching entry on every zone page of that family).
    /// The gamma page splits the frontier belt into its T0–T4 sections.
    /// (filename, markdown) pairs, ready to write next to map.md.</summary>
    public static List<(string File, string Md)> FamilyPages(Db db)
    {
        var zones = db.Query("SELECT id, name, zonetype, x, y, width, height, protected, terraformable, enabled FROM zones ORDER BY id")
            .Select(r => new Zone(r.Str("name"), r.Int("zonetype"), r.Dbl("x"), r.Dbl("y"),
                r.Int("width"), r.Int("height"), r.Bit("protected"), r.Bit("terraformable")))
            .ToList();
        var byName = zones.GroupBy(z => z.Name).ToDictionary(g => g.Key, g => g.First());
        var all = byName.Values.ToList();
        var groups = Groups(all);

        var pages = new List<(string, string)>();
        foreach (var g in groups.Where(g => g.Id != "training"))
        {
            var sb = new StringBuilder();
            var gamma = g.Id.StartsWith("t", StringComparison.Ordinal);
            var title = gamma ? "Gamma" : g.Label;
            var file = (gamma ? "gamma" : g.Id) + ".md";
            var desc = gamma
                ? "Every frontier-belt zone: the tc transit zones and the T1–T4 tier islands, each with its page."
                : $"Every {g.Id} zone — {(g.Id == "alpha" ? "the protected main islands and their PvE companions" : "the open-PvP islands")}, each with its own page.";
            sb.Append(Md.Header(title, desc, "zones (name, zonetype, width, height, protected, terraformable)",
                "family: " + (gamma ? "gamma" : g.Id)));
            sb.Append($"\n# {title}\n\n");
            if (gamma)
            {
                sb.Append("The frontier belt, split by the server-recorded tier (zones.note): the six tc transit zones (T0) and the tier islands T1–T4 — open PvP and terraformable, see [Protection levels](/zones/protection/).\n\n");
            }
            else
            {
                sb.Append(g.Blurb + "\n\n");
            }
            if (gamma)
            {
                foreach (var t in groups.Where(x => x.Id.StartsWith("t", StringComparison.Ordinal)))
                {
                    sb.Append($"<a id=\"{t.Id}\"></a>\n\n## {t.Label}\n\n{t.Blurb}\n\n");
                    sb.Append(Cards(t.Zones));
                }
            }
            else
            {
                sb.Append(Cards(g.Zones));
            }
            sb.Append("\n[World map](/zones/map/) · [Zone index](/zones/zone-index/) · [Protection levels](/zones/protection/)\n");
            pages.Add((file, sb.ToString()));
        }
        return pages;
    }

    /// <summary>A zone-card grid (thumb.png per zone) — the same cards the index
    /// page uses.</summary>
    private static string Cards(List<Zone> zones)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"zone-cards\">\n");
        foreach (var z in zones)
        {
            var slug = z.Name.ToLowerInvariant().Replace("_", "-");
            sb.Append($"<a class=\"zone-card\" href=\"/zones/{slug}/\">\n");
            sb.Append($"<img class=\"zone-card-thumb\" src=\"/zonemaps/{slug}/thumb.png\" alt=\"\" loading=\"lazy\">\n");
            sb.Append($"<span class=\"zone-card-name\">{Escape(ZoneName(z.Name))}</span>\n");
            sb.Append($"<span class=\"zone-card-meta\">{TypeLabel(z.Type)} · {z.W}×{z.H}</span>\n");
            sb.Append("</a>\n");
        }
        sb.Append("</div>\n\n");
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
                "The virtual [training island](/zones/zone-training/) — where new characters start and learn the basics before entering the main galaxies.",
                new List<Zone> { ByName("zone_training")! }),
            new("alpha", "Alpha",
                "The main protected islands and their second-wave PvE companions — the safe starter economy (see [Protection levels](/zones/protection/)).",
                all.Where(z => z.Name == "zone_TM" || z.Name == "zone_ICS" || z.Name == "zone_ASI"
                    || z.Name == "zone_TM_pve" || z.Name == "zone_ICS_pve" || z.Name == "zone_ASI_pve")
                    .OrderBy(z => z.Name, StringComparer.Ordinal).ToList()),
            new("beta", "Beta",
                "The open-PvP islands: the three main islands' PvP twins (the \"_real\" names), the eight gate islands of each galaxy, " +
                "and the special zones (the [PvP arena](/zones/zone-pvp-arena/) and the strongholds — protected instances, listed here for completeness).",
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
        List<(string Src, string Dst, string Rift, double? Gx, double? Gy)> links,
        List<(string Src, string Dst, int Tps, double? Sx, double? Sy, double? Tx, double? Ty)> tps,
        double vbW, double vbH, Func<double, double> Px, Func<double, double> Py)
    {
        // A connection line's endpoint sits at the real spot in the zone —
        // the TP column's / landing's tile position mapped into the island
        // node's rectangle (zone-local y grows downward, like the screen).
        (double X, double Y) End(Zone z, double? tx, double? ty)
        {
            var size = Size(z.W);
            var cx = Px(z.X);
            var cy = Py(z.Y);
            if (tx is null || ty is null || z.W == 0 || z.H == 0) return (cx, cy);
            return (cx + (tx.Value / z.W - 0.5) * size, cy + (ty.Value / z.H - 0.5) * size);
        }

        var sb = new StringBuilder();
        // Zones without any inter-zone teleport connection (the strongholds,
        // the PvP arena, dead-end gate islands) are left off the map: they add
        // noise without a line. A zone stays if it is a TP source or target.
        var connected = new HashSet<string>(
            tps.Select(t => t.Src).Concat(tps.Select(t => t.Dst)),
            StringComparer.Ordinal);
        var plotted = all.Where(z => connected.Contains(z.Name)).ToList();
        var plottedSet = new HashSet<string>(plotted.Select(z => z.Name), StringComparer.Ordinal);
        sb.Append($"<svg viewBox=\"0 0 {vbW:0} {vbH:0}\" role=\"img\" aria-label=\"Map of all game zones with teleport connections, at their grid positions, coastline outlines colored by galaxy family\" class=\"zonemap\" xmlns=\"http://www.w3.org/2000/svg\">\n");
        sb.Append("  <title>Map of all game zones with teleport connections, at their grid positions</title>\n");
        // Inter-zone TP lines, dashed and gradient-colored from the source
        // island's family color to the destination's (one gradient per line,
        // anchored in userSpace so the gradient follows the line direction).
        sb.Append("  <defs>\n");
        for (var i = 0; i < tps.Count; i++)
        {
            var l = tps[i];
            if (!plottedSet.Contains(l.Src) || !plottedSet.Contains(l.Dst)) continue;
            if (!byName.TryGetValue(l.Src, out var s) || !byName.TryGetValue(l.Dst, out var d)) continue;
            var (ax, ay) = End(s, l.Sx, l.Sy);
            var (bx, by) = End(d, l.Tx, l.Ty);
            sb.Append($"    <linearGradient id=\"tpg{i}\" gradientUnits=\"userSpaceOnUse\" x1=\"{ax:0.#}\" y1=\"{ay:0.#}\" x2=\"{bx:0.#}\" y2=\"{by:0.#}\">\n");
            sb.Append($"      <stop offset=\"0\" stop-color=\"{FamilyColor(l.Src)}\"/>\n");
            sb.Append($"      <stop offset=\"1\" stop-color=\"{FamilyColor(l.Dst)}\"/>\n");
            sb.Append("    </linearGradient>\n");
        }
        sb.Append("  </defs>\n");
        // Small islands first so the big-island labels draw on top. The
        // connection lines come AFTER the nodes: they run over the islands and
        // touch the columns'/landing spots' real positions inside them.
        foreach (var z in plotted.OrderBy(z => Size(z.W)).ThenBy(z => z.Name, StringComparer.Ordinal))
        {
            var f = Family(z.Name);
            var size = Size(z.W);
            var x1 = Px(z.X);
            var y1 = Py(z.Y);
            var shortName = z.Name.StartsWith("zone_", StringComparison.Ordinal) ? z.Name[5..] : z.Name;
            var tip = $"{z.Name} — {ProtectionLabel(z)} · {TypeLabel(z.Type)} ({z.X:0} / {z.Y:0})";
            var slug = z.Name.ToLowerInvariant().Replace("_", "-");
            // Same design as the index cards: the coastline-only thumbnail
            // (thumb.png — family-color island border on the dark plate) over
            // the family-outlined shape.
            var shape = f == "special"
                ? $"<circle cx=\"{x1:0.#}\" cy=\"{y1:0.#}\" r=\"{size / 2}\" fill=\"#10151f\" class=\"mapfam-{f} zonemap-node-{f}\"><title>{tip}</title></circle>"
                : $"<rect x=\"{x1 - size / 2:0.#}\" y=\"{y1 - size / 2:0.#}\" width=\"{size:0.#}\" height=\"{size:0.#}\" rx=\"{size * 0.3:0.#}\" fill=\"#10151f\" class=\"mapfam-{f} zonemap-node-{f}\"><title>{tip}</title></rect>";
            var image = $"<image href=\"/zonemaps/{slug}/thumb.png\" x=\"{x1 - size / 2:0.#}\" y=\"{y1 - size / 2:0.#}\" width=\"{size:0.#}\" height=\"{size:0.#}\" preserveAspectRatio=\"none\"/>";
            var label = f == "gamma" ? 10.5 : 13;
            var text = $"<text x=\"{x1 + size / 2 + 3:0.#}\" y=\"{y1 + label / 3:0.#}\" font-size=\"{label}\" class=\"zonemap-label\">{shortName}</text>";
            var href = PageLink(z.Name);
            sb.Append($"  <a href=\"{href}\"><g>{shape}{image}{text}</g></a>\n");
        }
        for (var i = 0; i < tps.Count; i++)
        {
            var l = tps[i];
            if (!plottedSet.Contains(l.Src) || !plottedSet.Contains(l.Dst)) continue;
            if (!byName.TryGetValue(l.Src, out var s) || !byName.TryGetValue(l.Dst, out var d)) continue;
            var (ax, ay) = End(s, l.Sx, l.Sy);
            var (bx, by) = End(d, l.Tx, l.Ty);
            sb.Append($"  <line x1=\"{ax:0.#}\" y1=\"{ay:0.#}\" x2=\"{bx:0.#}\" y2=\"{by:0.#}\" class=\"zonemap-tp\" stroke=\"url(#tpg{i})\">" +
                      $"<title>{l.Src} — {l.Dst} ({l.Tps} TP point{(l.Tps > 1 ? "s" : "")})</title></line>\n");
        }
        // Gate links only where the source island is on the map at all.
        foreach (var l in links)
        {
            if (!plottedSet.Contains(l.Src) || !plottedSet.Contains(l.Dst)) continue;
            if (!byName.TryGetValue(l.Src, out var s) || !byName.TryGetValue(l.Dst, out var d)) continue;
            var (ax, ay) = End(s, l.Gx, l.Gy);
            var (bx, by) = End(d, null, null);
            sb.Append($"  <line x1=\"{ax:0.#}\" y1=\"{ay:0.#}\" x2=\"{bx:0.#}\" y2=\"{by:0.#}\" class=\"zonemap-link\">" +
                      $"<title>{l.Src} — {l.Dst} (exit gate `{l.Rift}`)</title></line>\n");
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

    /// <summary>Same family colors as the zone teleport maps (ZoneMapSvg).</summary>
    private static string FamilyColor(string name) =>
        Family(name) switch
        {
            "tm" => "#41d3ff",
            "ics" => "#6ee7a0",
            "asi" => "#f5a05a",
            "gamma" => "#a78bfa",
            _ => "#c8d2e0",
        };

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

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
}
