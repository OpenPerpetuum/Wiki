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
    private sealed record Col(long Eid, double X, double Y, List<string> Dests, bool Enabled);
    private sealed record Spot(double X, double Y, string From);
    private sealed record Gate(double X, double Y, string To);
    /// <summary>One in-zone (local) teleport pair: both endpoints are teleport
    /// columns of the same zone. Rendered as a dashed line (hidden until the
    /// cursor comes near an endpoint — static/map.js) plus the data-ltp token
    /// on the two endpoint column circles.</summary>
    private sealed record LocalTp(long A, long B, double Ax, double Ay, double Bx, double By);

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

        // World-grid position of every zone (the same x/y the world map plots
        // each island at): the exit lines of a zone map point toward the
        // destination's position ON THE WORLD MAP. Sentinel rows (50000+) are
        // the duplicate placeholders — excluded, like in ZonesMapPage.
        var worldPos = db.Query("SELECT name, x, y FROM zones WHERE id < 49000 AND x < 49000 AND y < 49000")
            .GroupBy(r => r.Str("name"))
            .ToDictionary(g => g.Key, g => { var r = g.First(); return (X: r.Dbl("x"), Y: r.Dbl("y")); });

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

        // Local (in-zone) teleport pairs: teleportdescriptions rows where the
        // source and target zone are the same; each pair is recorded in both
        // directions, so deduplicate on the ordered (a, b) entity ids. The
        // endpoint columns must be among the plotted teleport entities.
        var colPos = cols.ToDictionary(c => (Zone: c.Zone, Eid: c.Eid), c => (X: c.X, Y: c.Y));
        var local = db.Query("""
            SELECT zn.name, td.sourcecolumn, td.targetcolumn
            FROM teleportdescriptions td
            JOIN zones zn ON zn.id = td.sourcezone
            WHERE zn.id = td.targetzone AND zn.id < 49000 AND td.active = 1
            """)
            .Select(r => (Zone: r.Str("name"),
                A: Math.Min((long)r.Lng("sourcecolumn"), (long)r.Lng("targetcolumn")),
                B: Math.Max((long)r.Lng("sourcecolumn"), (long)r.Lng("targetcolumn"))))
            .Where(t => t.A != t.B)
            .Distinct()
            .ToList();
        var localByZone = new Dictionary<string, List<LocalTp>>();
        foreach (var t in local)
        {
            if (!colPos.TryGetValue((t.Zone, t.A), out var a) || !colPos.TryGetValue((t.Zone, t.B), out var b)) continue;
            if (!localByZone.TryGetValue(t.Zone, out var list)) localByZone[t.Zone] = list = new List<LocalTp>();
            if (list.Any(l => l.A == t.A && l.B == t.B)) continue;
            list.Add(new LocalTp(t.A, t.B, a.X, a.Y, b.X, b.Y));
        }
        foreach (var list in localByZone.Values)
            list.Sort((l1, l2) => l1.A == l2.A ? l1.B.CompareTo(l2.B) : l1.A.CompareTo(l2.A));

        var result = new Dictionary<string, string>();
        var byZone = cols.GroupBy(c => c.Zone)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var (name, c) in byZone.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!size.TryGetValue(name, out var sz)) continue;
            var colList = c.Select(x => new Col(x.Eid, x.X, x.Y,
                destByColumn.TryGetValue(x.Eid, out var d) ? d : new List<string>(), x.Enabled)).ToList();
            var spotList = spots.Where(s => s.Zone == name).Select(s => new Spot(s.X, s.Y, s.From)).Distinct().ToList();
            var gateList = gates.Where(g => g.Str("name") == name).Select(g => new Gate(g.Dbl("x"), g.Dbl("y"), g.Str("dst"))).ToList();
            if (colList.Count == 0 && spotList.Count == 0 && gateList.Count == 0) continue;
            var ltpList = localByZone.TryGetValue(name, out var l) ? l : new List<LocalTp>();
            result[name] = Svg(name, sz.W, sz.H, colList, spotList, gateList, ltpList,
                worldPos.TryGetValue(name, out var selfPos) ? selfPos : default,
                worldPos);
        }
        return result;
    }

    private static string Svg(string name, int w, int h, List<Col> cols, List<Spot> spots, List<Gate> gates, List<LocalTp> localTps,
        (double X, double Y) selfPos, Dictionary<string, (double X, double Y)> worldPos)
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
        // Exit teleports: every column whose destination sits in ANOTHER zone
        // gets a dashed line from the column to the map edge, pointing the
        // way the destination lies on the WORLD MAP (world-grid delta, same
        // y-down orientation), colored like that destination's island there
        // (the family color scheme). The line leaves the map area — that is
        // the point: this column does not land inside this zone.
        if (selfPos.X != 0 || selfPos.Y != 0)
        {
            foreach (var c in cols.Where(c => c.Enabled))
            {
                foreach (var dest in c.Dests.Where(d => d != name).Distinct(StringComparer.Ordinal))
                {
                    if (!worldPos.TryGetValue(dest, out var dp)) continue;
                    double dx = dp.X - selfPos.X, dy = dp.Y - selfPos.Y;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < 1e-6) continue; // same spot on the world map
                    dx /= len; dy /= len;
                    // ray from the column to the zone-rect border
                    var t = double.MaxValue;
                    if (dx > 1e-9) t = Math.Min(t, (w - c.X) / dx);
                    else if (dx < -1e-9) t = Math.Min(t, -c.X / dx);
                    if (dy > 1e-9) t = Math.Min(t, (h - c.Y) / dy);
                    else if (dy < -1e-9) t = Math.Min(t, -c.Y / dy);
                    if (t == double.MaxValue || t <= 0) continue;
                    sb.Append($"  <line class=\"exi-line\" x1=\"{Fx(c.X)}\" y1=\"{Fx(c.Y)}\" x2=\"{Fx(c.X + dx * t)}\" y2=\"{Fx(c.Y + dy * t)}\" " +
                              $"stroke=\"{FamilyColor(dest)}\" stroke-width=\"{3 * f}\" stroke-dasharray=\"{Fx(16 * f)} {Fx(12 * f)}\" opacity=\"0.6\"/>\n");
                }
            }
        }
        // local (in-zone) teleport pairs: a dashed line per pair, over a dark
        // casing line so the dashes read on the bright color-mode terrain —
        // static/map.js lights the colored line up while the cursor is near
        // one of the two endpoint columns (the circles below carry the same
        // data-ltp token).
        for (var i = 0; i < localTps.Count; i++)
        {
            var l = localTps[i];
            // self-closing (no <title> child): tools/gen_zone_teleport_maps.py
            // passes self-closing <line> elements through, but a line with
            // children would fall through its item parser and be dropped
            sb.Append($"  <line class=\"ltp-line ltp-casing zm-casing\" x1=\"{Fx(l.Ax)}\" y1=\"{Fx(l.Ay)}\" x2=\"{Fx(l.Bx)}\" y2=\"{Fx(l.By)}\" " +
                      $"stroke=\"#0a0f18\" stroke-width=\"{6 * f}\" stroke-dasharray=\"{Fx(12 * f)} {Fx(9 * f)}\" opacity=\"0.55\"/>\n");
            sb.Append($"  <line class=\"ltp-line\" data-ltp=\"{i}\" x1=\"{Fx(l.Ax)}\" y1=\"{Fx(l.Ay)}\" x2=\"{Fx(l.Bx)}\" y2=\"{Fx(l.By)}\" " +
                      $"stroke=\"{FamilyColor(name)}\" stroke-width=\"{3 * f}\" stroke-dasharray=\"{Fx(12 * f)} {Fx(9 * f)}\"/>\n");
        }
        // landing spots first (under the columns); the label joins every
        // distinct origin sharing the same spot.
        foreach (var g in spots.GroupBy(s => (s.X, s.Y)))
        {
            var x = g.Key.Item1;
            var y = g.Key.Item2;
            var from = g.Select(s => Disp(s.From)).Distinct(StringComparer.Ordinal).ToList();
            var label = from.Count <= 2 ? string.Join(", ", from) : string.Join(", ", from.Take(2)) + "…";
            sb.Append($"  <circle class=\"zm-casing\" cx=\"{Fx(x)}\" cy=\"{Fx(y)}\" r=\"{Fx(r * 1.7)}\" fill=\"none\" stroke=\"#0a0f18\" stroke-width=\"{5 * f}\" stroke-dasharray=\"{Fx(7 * f)} {Fx(5 * f)}\" opacity=\"0.55\"/>\n");
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
        // teleport columns, sorted for determinism; disabled ones dimmer.
        // Columns that are an endpoint of a local (in-zone) teleport carry the
        // data-ltp token(s) so the hover lines can find their endpoints.
        var ltpByEid = new Dictionary<long, List<int>>();
        for (var i = 0; i < localTps.Count; i++)
        {
            if (!ltpByEid.TryGetValue(localTps[i].A, out var la)) ltpByEid[localTps[i].A] = la = new List<int>();
            la.Add(i);
            if (!ltpByEid.TryGetValue(localTps[i].B, out var lb)) ltpByEid[localTps[i].B] = lb = new List<int>();
            lb.Add(i);
        }
        foreach (var c in cols.OrderBy(c => c.Enabled ? 0 : 1).ThenBy(c => c.X).ThenBy(c => c.Y))
        {
            // local (in-zone) teleport endpoints point at the zone itself —
            // labeling them would just repeat the page title, so the label
            // lists only destinations in OTHER zones (the color keeps using
            // the full destination list).
            var dests = c.Dests.Where(d => d != name).Select(Disp).Distinct(StringComparer.Ordinal).ToList();
            var label = dests.Count == 0 ? "" :
                dests.Count <= 2 ? string.Join(", ", dests) : dests[0] + ", " + dests[1] + "…";
            var color = c.Enabled ? ColorFor(label, c.Dests) : "#5b6478";
            var below = c.Y < fs * 3;
            var ty = below ? c.Y + r + fs : c.Y - r - fs * 0.5;
            var ltpAttr = ltpByEid.TryGetValue(c.Eid, out var toks) && toks.Count > 0
                ? $" data-ltp=\"{string.Join(" ", toks)}\""
                : "";
            sb.Append($"  <circle cx=\"{Fx(c.X)}\" cy=\"{Fx(c.Y)}\" r=\"{Fx(r)}\"{ltpAttr} fill=\"{color}\" stroke=\"#10151f\" stroke-width=\"{2 * f}\" opacity=\"{(c.Enabled ? 1 : 0.55)}\"/>\n");
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

    /// <summary>The zone's own family color (local teleport lines).</summary>
    private static string FamilyColor(string zoneName)
    {
        if (zoneName.Contains("tm", StringComparison.OrdinalIgnoreCase)) return "#41d3ff";
        if (zoneName.Contains("ics", StringComparison.OrdinalIgnoreCase)) return "#6ee7a0";
        if (zoneName.Contains("asi", StringComparison.OrdinalIgnoreCase)) return "#f5a05a";
        if (zoneName.Contains("gamma", StringComparison.OrdinalIgnoreCase)) return "#a78bfa";
        return "#c8d2e0";
    }

    private static string Disp(string internalName)
        => Md.ClientStrings.TryGetValue(internalName, out var d) && d != internalName ? d : internalName;

    private static string Fx(double v) => Math.Round(v, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
