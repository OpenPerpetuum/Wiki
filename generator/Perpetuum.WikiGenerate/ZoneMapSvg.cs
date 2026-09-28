namespace Perpetuum.WikiGenerate;

/// <summary>
/// One small SVG per zone (static/zonemaps/&lt;zone-slug&gt;.svg) rendering the
/// zone's own extent with its teleport columns plotted at their real tile
/// positions (from zoneentities), the landing spots of teleports arriving from
/// other zones (targetx/targety from teleportdescriptions), and the exit gates
/// of strongholds/arenas (strongholdexitconfig). The generator embeds the
/// image on each zone page.
/// </summary>
public static class ZoneMapSvg
{
    private sealed record Col(double X, double Y, List<string> Dests, bool Enabled);
    private sealed record Spot(double X, double Y, string From);
    private sealed record Gate(double X, double Y, string To);

    /// <summary>
    /// zone name -> SVG markup, for every zone with any teleport data. Columns are
    /// plotted whether or not they are enabled: the disabled ones (every TP column
    /// of zone_ASI) are drawn dimmer and listed in the caption data.
    /// </summary>
    public static Dictionary<string, string> Build(Db db)
    {
        // Zone size of each real zone row (sentinel duplicates excluded).
        var size = db.Query("SELECT id, name, width, height FROM zones WHERE id < 49000")
            .GroupBy(r => r.Str("name"))
            .ToDictionary(g => g.Key, g => { var r = g.First(); return (W: r.Int("width"), H: r.Int("height")); });

        // Teleport columns that exist as enabled zone entities.
        var cols = db.Query("""
            SELECT zn.name, e.eid, z.x, z.y, z.enabled
            FROM zones zn
            JOIN zoneentities z ON z.zoneid = zn.id
            JOIN entities e ON e.eid = z.eid
            WHERE (e.ename LIKE 'tpc_%' OR e.ename LIKE 'tp_zone_%'
                   OR LOWER(e.ename) LIKE 'teleport_column%'
                   OR e.ename LIKE 'tp_train_%' OR e.ename LIKE 'training_tp_%')
            AND zn.id < 49000
            """)
            .Select(r => (Zone: r.Str("name"), Eid: (long)r.Lng("eid"), X: r.Dbl("x"), Y: r.Dbl("y"), Enabled: r.Bit("enabled")))
            .ToList();

        // Active routes: source column -> destination zones, and landing spots.
        var routes = db.Query("""
            SELECT zs.name AS src, zd.name AS dst, td.sourcecolumn,
                   td.targetx, td.targety, td.active, td.listable
            FROM teleportdescriptions td
            JOIN zones zs ON zs.id = td.sourcezone
            JOIN zones zd ON zd.id = td.targetzone
            WHERE zs.id < 49000 AND zd.id < 49000
            """)
            .ToList();

        var destByColumn = new Dictionary<long, List<string>>();
        var spotSet = new HashSet<(string Zone, double X, double Y, string From)>();
        var spots = new List<(string Zone, double X, double Y, string From)>();
        foreach (var r in routes.Where(r => r.Bit("active") && r.Bit("listable")))
        {
            var eid = (long)r.Lng("sourcecolumn");
            if (!destByColumn.TryGetValue(eid, out var list)) destByColumn[eid] = list = new List<string>();
            var dst = r.Str("dst");
            if (!list.Contains(dst, StringComparer.Ordinal)) list.Add(dst);
            if (r.TryGetValue("targetx", out var tx) && tx is not null
                && r.TryGetValue("targety", out var ty) && ty is not null)
            {
                var spot = (Zone: r.Str("dst"), X: (double)tx, Y: (double)ty, From: r.Str("src"));
                if (spotSet.Add(spot)) spots.Add(spot);
            }
        }

        var gates = db.Query("""
            SELECT zn.name, se.x, se.y, dz.name AS dst
            FROM strongholdexitconfig se
            JOIN zones zn ON zn.id = se.zoneid
            JOIN riftconfigs rc ON rc.id = se.riftConfigId
            JOIN riftdestinations rd ON rd.groupId = rc.destinationGroupId
            JOIN zones dz ON dz.id = rd.zoneId
            WHERE zn.id < 49000
            """)
            .ToList();

        var result = new Dictionary<string, string>();
        var byZone = cols.GroupBy(c => c.Zone)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var (name, c) in byZone.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!size.TryGetValue(name, out var sz)) continue;
            var colList = c.Select(x => new Col(x.X, x.Y,
                destByColumn.TryGetValue(x.Eid, out var d) ? d : new List<string>(), x.Enabled)).ToList();
            var spotList = spots.Where(s => s.Zone == name).Select(s => new Spot(s.X, s.Y, s.From)).Distinct().ToList();
            var gateList = gates.Where(g => g.Str("name") == name).Select(g => new Gate(g.Dbl("x"), g.Dbl("y"), g.Str("dst"))).ToList();
            if (colList.Count == 0 && spotList.Count == 0 && gateList.Count == 0) continue;
            result[name] = Svg(name, sz.W, sz.H, colList, spotList, gateList);
        }
        return result;
    }

    private static string Svg(string name, int w, int h, List<Col> cols, List<Spot> spots, List<Gate> gates)
    {
        var f = (double)w / 2048.0; // scale font/radii with the zone size
        var r = 12 * f;
        var fs = 26 * f;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w} {h}\" width=\"640\" height=\"{(int)(640.0 * h / w)}\" role=\"img\" aria-label=\"Teleport map of {Escape(name)}\">\n");
        sb.Append($"  <rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"{16 * f}\" fill=\"#10151f\" stroke=\"#39445a\" stroke-width=\"{2 * f}\"/>\n");
        // subtle 8x8 grid
        for (var i = 1; i < 8; i++)
        {
            var gx = w / 8.0 * i;
            var gy = h / 8.0 * i;
            sb.Append($"  <line x1=\"{Fx(gx)}\" y1=\"0\" x2=\"{Fx(gx)}\" y2=\"{h}\" stroke=\"#1d2534\" stroke-width=\"{1 * f}\"/>\n");
            sb.Append($"  <line x1=\"0\" y1=\"{Fx(gy)}\" x2=\"{w}\" y2=\"{Fx(gy)}\" stroke=\"#1d2534\" stroke-width=\"{1 * f}\"/>\n");
        }
        // landing spots first (under the columns); the label joins every
        // distinct origin sharing the same spot.
        foreach (var g in spots.GroupBy(s => (s.X, s.Y)))
        {
            var x = g.Key.Item1;
            var y = g.Key.Item2;
            var from = g.Select(s => Disp(s.From)).Distinct(StringComparer.Ordinal).ToList();
            var label = from.Count <= 2 ? string.Join(", ", from) : string.Join(", ", from.Take(2)) + "…";
            sb.Append($"  <circle cx=\"{Fx(x)}\" cy=\"{Fx(y)}\" r=\"{Fx(r * 1.7)}\" fill=\"none\" stroke=\"#c8d2e0\" stroke-width=\"{2 * f}\" stroke-dasharray=\"{Fx(7 * f)} {Fx(5 * f)}\" opacity=\"0.85\"/>\n");
            sb.Append($"  <text x=\"{Fx(x)}\" y=\"{Fx(y - r * 2.4)}\" font-size=\"{Fx(fs * 0.8)}\" fill=\"#8b93a5\" text-anchor=\"middle\" font-family=\"sans-serif\">from {Escape(label)}</text>\n");
        }
        // exit gates (diamonds)
        foreach (var g in gates)
        {
            var d = r * 1.6;
            sb.Append($"  <path d=\"M {Fx(g.X)} {Fx(g.Y - d)} L {Fx(g.X + d)} {Fx(g.Y)} L {Fx(g.X)} {Fx(g.Y + d)} L {Fx(g.X - d)} {Fx(g.Y)} Z\" fill=\"#f5a05a\" stroke=\"#10151f\" stroke-width=\"{2 * f}\"/>\n");
            sb.Append($"  <text x=\"{Fx(g.X)}\" y=\"{Fx(g.Y + d + fs)}\" font-size=\"{Fx(fs * 0.8)}\" fill=\"#d5dbe8\" text-anchor=\"middle\" font-family=\"sans-serif\">exit → {Escape(Disp(g.To))}</text>\n");
        }
        // teleport columns, sorted for determinism; disabled ones dimmer
        foreach (var c in cols.OrderBy(c => c.Enabled ? 0 : 1).ThenBy(c => c.X).ThenBy(c => c.Y))
        {
            var dests = c.Dests.Select(Disp).Distinct(StringComparer.Ordinal).ToList();
            var label = dests.Count == 0 ? "" :
                dests.Count <= 2 ? string.Join(", ", dests) : dests[0] + ", " + dests[1] + "…";
            var color = c.Enabled ? ColorFor(label, c.Dests) : "#5b6478";
            var below = c.Y < fs * 3;
            var ty = below ? c.Y + r + fs : c.Y - r - fs * 0.5;
            sb.Append($"  <circle cx=\"{Fx(c.X)}\" cy=\"{Fx(c.Y)}\" r=\"{Fx(r)}\" fill=\"{color}\" stroke=\"#10151f\" stroke-width=\"{2 * f}\" opacity=\"{(c.Enabled ? 1 : 0.55)}\"/>\n");
            if (label.Length > 0)
                sb.Append($"  <text x=\"{Fx(c.X)}\" y=\"{Fx(ty)}\" font-size=\"{Fx(fs)}\" fill=\"#d5dbe8\" text-anchor=\"middle\" font-family=\"sans-serif\" opacity=\"{(c.Enabled ? 1 : 0.55)}\">{Escape(label)}</text>\n");
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    /// <summary>Same family colors as the main zone map.</summary>
    private static string ColorFor(string label, List<string> dests)
    {
        if (dests.Count == 0) return "#8b93a5";
        var n = dests[0];
        if (n.Contains("tm", StringComparison.OrdinalIgnoreCase)) return "#41d3ff";
        if (n.Contains("ics", StringComparison.OrdinalIgnoreCase)) return "#6ee7a0";
        if (n.Contains("asi", StringComparison.OrdinalIgnoreCase)) return "#f5a05a";
        if (n.Contains("gamma", StringComparison.OrdinalIgnoreCase)) return "#a78bfa";
        return "#c8d2e0";
    }

    private static string Disp(string internalName)
        => Md.ClientStrings.TryGetValue(internalName, out var d) && d != internalName ? d : internalName;

    private static string Fx(double v) => Math.Round(v, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
