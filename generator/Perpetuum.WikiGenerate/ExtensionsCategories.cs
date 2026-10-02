namespace Perpetuum.WikiGenerate;

/// <summary>
/// Main-categories overview SVG (static/extensions-categories.svg) for the
/// extensions page: one box per extension category (extension count, entry
/// points without prerequisites, rank range), three columns by starting
/// rank, and an arrow for every cross-category prerequisite (which
/// categories open up which, without the full detail tree). Mirrors the
/// Python tool tools/gen_extension_categories.py — keep the two in sync.
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
            var d = cat.TryGetValue(r.Int("category"), out var v) ? v : (0, 0, 99, 0);
            cat[r.Int("category")] = (d.Count + 1, d.Roots, Math.Min(d.MinR, r.Int("rank")), Math.Max(d.MaxR, r.Int("rank")));
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

        // Columns by starting rank: 1 -> foundation, 2..9 -> advanced, 10 -> spark.
        var col0 = cats.Keys.Where(c => cat[c].MinR <= 1).OrderBy(c => c).ToList();
        var col1 = cats.Keys.Where(c => cat[c].MinR is >= 2 and < 10).OrderBy(c => c).ToList();
        var col2 = cats.Keys.Where(c => cat[c].MinR >= 10).OrderBy(c => c).ToList();
        var cols = new[] { col0, col1, col2 };
        var colTitles = new[] { "Starting at rank 1", "Building on the core skills", "Spark extensions" };

        const int BW = 250, BH = 74, GX = 70, GY = 26, MX = 24, MY = 46;
        var ncol = cols.Max(c => c.Count);
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

        var color = new Dictionary<int, string>();
        var pi = 0;
        foreach (var c in cats.Keys.OrderBy(c => c))
            color[c] = Palette[pi++ % Palette.Length];

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {W} {H}\" width=\"1280\" " +
                  $"height=\"{(int)(1280.0 * H / W)}\" role=\"img\" " +
                  $"aria-label=\"Extension categories: {cats.Count} categories with cross-category prerequisite edges\">\n");
        sb.Append($"  <rect x=\"0\" y=\"0\" width=\"{W}\" height=\"{H}\" fill=\"#10151f\" stroke=\"#39445a\" stroke-width=\"1\"/>\n");
        for (var ci = 0; ci < 3; ci++)
            sb.Append($"  <text x=\"{MX + ci * (BW + GX) + BW / 2}\" y=\"{MY - 16}\" font-size=\"13\" fill=\"#8b93a5\" " +
                      $"text-anchor=\"middle\" font-family=\"sans-serif\">{Escape(colTitles[ci])}</text>\n");
        sb.Append("  <defs><marker id=\"arrc\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"7\" " +
                  "markerHeight=\"7\" orient=\"auto-start-reverse\">" +
                  "<path d=\"M 0 0 L 10 5 L 0 10 z\" fill=\"#8b93a5\"/></marker></defs>\n");
        // Edges (under the boxes): the requiring category points at the required one.
        foreach (var ((fc, tc), detail) in edges.OrderBy(e => e.Key))
        {
            if (!pos.TryGetValue(fc, out var (fx, fy)) || !pos.TryGetValue(tc, out var (tx, ty)))
                continue;
            var tip = string.Join("; ", detail.Select(d => $"{d.Ext} requires {d.Req} \u2265{d.Lvl}"));
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
        // Category boxes.
        foreach (var c in cats.Keys.OrderBy(k => k))
        {
            var d = cat[c];
            var (x, y) = pos[c];
            var raw = cats[c];
            var label = (raw.StartsWith("extcat_") ? raw["extcat_".Length..] : raw).Replace('_', ' ');
            sb.Append($"  <g><title>{Escape(label)}: {d.Count} extensions, {d.Roots} entry points, " +
                      $"rank {d.MinR}–{d.MaxR}</title>");
            sb.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"{BW}\" height=\"{BH}\" rx=\"8\" fill=\"#1a2233\" " +
                      $"stroke=\"{color[c]}\" stroke-width=\"1.5\"/>");
            sb.Append($"<text x=\"{x + 12}\" y=\"{y + 24}\" font-size=\"14\" font-weight=\"bold\" fill=\"#e8ecf4\" " +
                      $"font-family=\"sans-serif\">{Escape(label)}</text>");
            sb.Append($"<text x=\"{x + 12}\" y=\"{y + 46}\" font-size=\"11.5\" fill=\"#aab2c5\" font-family=\"sans-serif\">" +
                      $"{d.Count} extensions · {d.Roots} entry point{(d.Roots == 1 ? "" : "s")}</text>");
            sb.Append($"<text x=\"{x + 12}\" y=\"{y + 63}\" font-size=\"11.5\" fill=\"#8b93a5\" font-family=\"sans-serif\">" +
                      $"rank {d.MinR}–{d.MaxR}</text>");
            sb.Append("</g>\n");
        }
        sb.Append("</svg>\n");
        return (sb.ToString(), cats.Count, edges.Values.Sum(v => v.Count));
    }

    private static string Escape(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
