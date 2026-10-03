namespace Perpetuum.WikiGenerate;

/// <summary>
/// Main-categories overview SVG (static/extensions-categories.svg) for the
/// extensions page: one box per extension category (extension count, entry
/// points without prerequisites, rank range), the columns ordered left to
/// right by the starting rank (the progression reads left to right), and an
/// arrow for every cross-category prerequisite (which categories open up
/// which, without the full detail tree). The spark-extension category sits
/// outside this diagram (no prerequisites of its own) — it has its own
/// diagram on the sparks page (SparksTree). Mirrors the Python tool
/// tools/gen_extension_categories.py — keep the two in sync.
/// </summary>
public static class ExtensionsCategories
{
    private static readonly string[] Palette =
    {
        "#41d3ff", "#6ee7a0", "#f5a05a", "#a78bfa", "#f472b6", "#facc15",
        "#38bdf8", "#fb7185", "#4ade80", "#e879f9", "#fdba74", "#93c5fd",
        "#a3e635", "#22d3ee", "#f472b6",
    };

    public static (string Svg, int Categories, int Edges) Build(Db db)
    {
        var cats = db.Query("SELECT extensioncategoryid, categoryname FROM extensioncategories")
            .ToDictionary(r => r.Int("extensioncategoryid"), r => r.Str("categoryname"));
        var exts = db.Query("""
            SELECT extensionid, extensionname, category, rank
            FROM extensions WHERE active = 1
            """).ToList();
        var id2name = exts.ToDictionary(r => r.Int("extensionid"), r => r.Str("extensionname"));
        var id2cat = exts.ToDictionary(r => r.Int("extensionid"), r => r.Int("category"));
        // extensionprerequire stores ids, like the detail tree query.
        var prereqRows = db.Query("SELECT extensionid, requiredextension, requiredlevel FROM extensionprerequire")
            .ToList();

        // Per-category stats (active extensions only, like the detail tree).
        var cat = new Dictionary<int, (int Count, int Roots, int MinR, int MaxR)>();
        foreach (var r in exts)
        {
            var c = r.Int("category");
            var rank = r.Int("rank");
            if (!cat.TryGetValue(c, out var d))
                cat[c] = (1, 0, rank, rank);
            else
                cat[c] = (d.Count + 1, d.Roots, Math.Min(d.MinR, rank), Math.Max(d.MaxR, rank));
        }
        foreach (var r in exts)
        {
            if (!prereqRows.Any(p => p.Int("extensionid") == r.Int("extensionid")))
            {
                var d = cat[r.Int("category")];
                cat[r.Int("category")] = (d.Count, d.Roots + 1, d.MinR, d.MaxR);
            }
        }

        // Cross-category prerequisite edges: requiring category -> required category.
        var edges = new Dictionary<(int From, int To), List<(string Ext, string Req, int Lvl)>>();
        foreach (var p in prereqRows)
        {
            var fromId = p.Int("extensionid");
            var toId = p.Int("requiredextension");
            if (!id2cat.TryGetValue(fromId, out var from) || !id2cat.TryGetValue(toId, out var to) || from == to)
                continue;
            if (!edges.TryGetValue((from, to), out var list))
                edges[(from, to)] = list = new List<(string, string, int)>();
            list.Add((id2name[fromId], id2name.GetValueOrDefault(toId, toId.ToString()), p.Int("requiredlevel")));
        }

        // The spark category has its own diagram (sparks page): excluded here.
        var sparkCats = cats.Where(kv => kv.Value.StartsWith("extcat_spark", StringComparison.Ordinal)).Select(kv => kv.Key).ToHashSet();
        // Categories with no active extensions (retired ids) have no stats: skip.
        var laid = cats.Keys.Where(c => cat.ContainsKey(c) && !sparkCats.Contains(c)).ToList();
        // Columns ordered left to right by the starting rank: the progression
        // (rank 1 skills -> rank 2+ -> rank 4+ skills) reads left to right.
        var col0 = laid.Where(c => cat[c].MinR <= 1).OrderBy(c => cat[c].MinR).ThenBy(c => cats[c], StringComparer.Ordinal).ToList();
        var col1 = laid.Where(c => cat[c].MinR is >= 2 and <= 3).OrderBy(c => cat[c].MinR).ThenBy(c => cats[c], StringComparer.Ordinal).ToList();
        var col2 = laid.Where(c => cat[c].MinR is >= 4 and < 10).OrderBy(c => cat[c].MinR).ThenBy(c => cats[c], StringComparer.Ordinal).ToList();
        var cols = new[] { col0, col1, col2 };
        var colTitles = new[] { "Starting at rank 1", "Building on rank 2", "Building on rank 4+" };

        const int BW = 250, BH = 74, GX = 70, GY = 26, MX = 24, MY = 46;
        var ncol = Math.Max(1, cols.Max(c => c.Count));
        var W = MX * 2 + 3 * BW + 2 * GX;
        var H = MY * 2 + ncol * BH + (ncol - 1) * GY;
        var pos = new Dictionary<int, (int X, int Y)>();
        for (var ci = 0; ci < 3; ci++)
        {
            var colh = cols[ci].Count * BH + Math.Max(0, cols[ci].Count - 1) * GY;
            var y0 = MY + (H - 2 * MY - colh) / 2;
            for (var ri = 0; ri < cols[ci].Count; ri++)
                pos[cols[ci][ri]] = (MX + ci * (BW + GX), y0 + ri * (BH + GY));
        }

        // Color assignment by display name over the categories that actually
        // have active extensions (matches the Python tool).
        var color = new Dictionary<int, string>();
        var pi = 0;
        foreach (var c in cat.Keys.OrderBy(k => Md.DisplayName(cats[k]), StringComparer.Ordinal))
            color[c] = Palette[pi++ % Palette.Length];

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {W} {H}\" width=\"1280\" " +
                  $"height=\"{(int)(1280.0 * H / W)}\" role=\"img\" " +
                  $"aria-label=\"Extension categories: {cat.Count} categories with cross-category prerequisite edges\">\n");
        sb.Append($"  <rect x=\"0\" y=\"0\" width=\"{W}\" height=\"{H}\" fill=\"#10151f\" stroke=\"#39445a\" stroke-width=\"1\"/>\n");
        for (var ci = 0; ci < 3; ci++)
            sb.Append($"  <text x=\"{MX + ci * (BW + GX) + BW / 2}\" y=\"{MY - 16}\" font-size=\"13\" fill=\"#8b93a5\" " +
                      $"text-anchor=\"middle\" font-family=\"sans-serif\">{Escape(colTitles[ci])}</text>\n");
        sb.Append("  <defs><marker id=\"arrc\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"7\" " +
                  "markerHeight=\"7\" orient=\"auto-start-reverse\">" +
                  "<path d=\"M 0 0 L 10 5 L 0 10 z\" fill=\"#8b93a5\"/></marker></defs>\n");
        // Edges (under the boxes): the requiring category points at the required one.
        foreach (var ((fc, tc), detail) in edges
            .OrderBy(e => Md.DisplayName(cats[e.Key.From]), StringComparer.Ordinal)
            .ThenBy(e => Md.DisplayName(cats[e.Key.To]), StringComparer.Ordinal))
        {
            if (!pos.TryGetValue(fc, out var fp) || !pos.TryGetValue(tc, out var tp))
                continue;
            var (fx, fy) = fp;
            var (tx, ty) = tp;
            var tip = string.Join("; ", detail
                .OrderBy(d => Md.DisplayName(d.Req), StringComparer.Ordinal)
                .ThenBy(d => Md.DisplayName(d.Ext), StringComparer.Ordinal)
                .Select(d => $"{Md.DisplayName(d.Ext)} requires {Md.DisplayName(d.Req)} \u2265{d.Lvl}"));
            string d;
            double lx, ly;
            if (fx == tx)
            {
                lx = fx + BW / 2 + 12;
                var sx = fx + BW / 2;
                if (fy < ty)
                {
                    d = $"M {sx} {fy + BH} L {sx} {ty - 2}";
                    ly = (fy + BH + ty) / 2;
                }
                else
                {
                    d = $"M {sx} {fy} L {sx} {ty + BH - 2}";
                    ly = (fy + ty + BH) / 2;
                }
            }
            else if (fx < tx)
            {
                var sx = fx + BW; var sy = fy + BH / 2.0;
                var ex = tx; var ey = ty + BH / 2.0;
                var mx = (sx + ex) / 2;
                d = $"M {sx} {sy} C {mx} {sy}, {mx} {ey}, {ex - 2} {ey}";
                lx = mx; ly = (sy + ey) / 2 - 4;
            }
            else
            {
                var sx = fx; var sy = fy + BH / 2.0;
                var ex = tx + BW; var ey = ty + BH / 2.0;
                var mx = (sx + ex) / 2;
                d = $"M {sx} {sy} C {mx} {sy}, {mx} {ey}, {ex - 2} {ey}";
                lx = mx; ly = (sy + ey) / 2 - 4;
            }
            sb.Append($"  <path d=\"{d}\" fill=\"none\" stroke=\"{color[fc]}\" stroke-width=\"1.4\" " +
                      $"stroke-opacity=\"0.55\" marker-end=\"url(#arrc)\"><title>{Escape(tip)}</title></path>\n");
            sb.Append($"  <text x=\"{lx}\" y=\"{ly}\" font-size=\"11\" fill=\"{color[fc]}\" " +
                      $"text-anchor=\"middle\" font-family=\"sans-serif\">{detail.Count}×</text>\n");
        }
        // Category boxes (only the laid-out ones; retired/spark categories have no box).
        foreach (var c in pos.Keys.OrderBy(k => Md.DisplayName(cats[k]), StringComparer.Ordinal))
        {
            var d = cat[c];
            var (x, y) = pos[c];
            var label = Md.DisplayName(cats[c]);
            // The box is a link to its category section on the extensions
            // page (the SVG is embedded inline there, so <a href="#…"> works).
            var anchor = "#cat-" + ExtensionsPage.SlugCat(cats[c]);
            sb.Append($"  <g><title>{Escape(label)}: {d.Count} extensions, {d.Roots} entry points, " +
                      $"rank {d.MinR}–{d.MaxR}</title><a href=\"{anchor}\">");
            sb.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"{BW}\" height=\"{BH}\" rx=\"8\" fill=\"#1a2233\" " +
                      $"stroke=\"{color[c]}\" stroke-width=\"1.5\"/>");
            sb.Append($"<text x=\"{x + 12}\" y=\"{y + 24}\" font-size=\"14\" font-weight=\"bold\" fill=\"#e8ecf4\" " +
                      $"font-family=\"sans-serif\">{Escape(label)}</text>");
            sb.Append($"<text x=\"{x + 12}\" y=\"{y + 46}\" font-size=\"11.5\" fill=\"#aab2c5\" font-family=\"sans-serif\">" +
                      $"{d.Count} extensions · {d.Roots} entry point{(d.Roots == 1 ? "" : "s")}</text>");
            sb.Append($"<text x=\"{x + 12}\" y=\"{y + 63}\" font-size=\"11.5\" fill=\"#8b93a5\" font-family=\"sans-serif\">" +
                      $"rank {d.MinR}–{d.MaxR}</text>");
            sb.Append("</a></g>\n");
        }
        sb.Append("</svg>\n");
        // Count the categories that actually have active extensions (the table
        // also carries retired category ids), sparks included.
        return (sb.ToString(), cat.Count, edges.Values.Sum(v => v.Count));
    }

    private static string Escape(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
