namespace Perpetuum.WikiGenerate;

/// <summary>
/// Interactive extension-tree SVG (static/extensions-tree.svg), embedded on the
/// extensions page with the same zoom/pan wrapper as the world map
/// (.map-zoom-wrap / .zoommap in static/map.js). One column per rank (1..10),
/// one box per extension, rows grouped by category; edges show prerequisites
/// (required level in the tooltip).
/// </summary>
public static class ExtensionsTree
{
    private sealed record Node(int Id, string Name, int Category, int Rank);

    public static (string Svg, int Nodes, int Edges) Build(Db db)
    {
        var cats = db.Query("SELECT extensioncategoryid, categoryname FROM extensioncategories")
            .ToDictionary(r => r.Int("extensioncategoryid"), r => r.Str("categoryname"));
        var nodes = db.Query("""
            SELECT extensionid, extensionname, category, rank
            FROM extensions WHERE active = 1
            """)
            .Select(r => new Node(r.Int("extensionid"), r.Str("extensionname"), r.Int("category"), r.Int("rank")))
            .ToList();
        var byId = nodes.ToDictionary(n => n.Id);
        var edges = db.Query("SELECT extensionid, requiredextension, requiredlevel FROM extensionprerequire")
            .Select(r => (From: r.Int("extensionid"), To: r.Int("requiredextension"), Lvl: r.Int("requiredlevel")))
            .Where(e => byId.ContainsKey(e.From) && byId.ContainsKey(e.To))
            .ToList();

        // Layout: x by rank, y by (category, rank, name) order.
        var W = 190;
        var H = 30;
        var GX = 60;
        var GY = 8;
        var MARGIN = 16;
        var catIds = cats.Keys.OrderBy(c => c).ToList();
        var pos = new Dictionary<int, (int X, int Y)>();
        var catBand = new Dictionary<int, (int Top, int Bottom, string Name)>();
        var y = MARGIN;
        foreach (var cat in catIds)
        {
            var members = nodes.Where(n => n.Category == cat).OrderBy(n => n.Rank).ThenBy(n => n.Name).ToList();
            if (members.Count == 0) continue;
            var top = y;
            var maxRank = members.Max(m => m.Rank);
            foreach (var rank in Enumerable.Range(1, maxRank))
            {
                var row = members.Where(m => m.Rank == rank).ToList();
                foreach (var m in row)
                    pos[m.Id] = (MARGIN + (m.Rank - 1) * (W + GX), y);
                y += H + GY;
            }
            y += 14; // gap between categories
            catBand[cat] = (top, y, cats[cat]);
        }
        var width = MARGIN * 2 + 10 * (W + GX) - GX;
        var height = y + MARGIN;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" width=\"1280\" height=\"{(int)(1280.0 * height / width)}\" role=\"img\" aria-label=\"Extension tree: {nodes.Count} extensions arranged by rank and category, with prerequisite edges\">\\n");
        sb.Append($"  <rect x=\"0\" y=\"0\" width=\"{width}\" height=\"{height}\" fill=\"#10151f\" stroke=\"#39445a\" stroke-width=\"1\"/>\\n");
        // rank headers
        for (var rank = 1; rank <= 10; rank++)
            sb.Append($"  <text x=\"{MARGIN + (rank - 1) * (W + GX) + W / 2}\" y=\"{MARGIN - 4}\" font-size=\"13\" fill=\"#8b93a5\" text-anchor=\"middle\" font-family=\"sans-serif\">rank {rank}</text>\\n");
        // category bands + labels
        foreach (var (cat, band) in catBand.OrderBy(kv => kv.Value.Top))
        {
            var midY = (band.Top + band.Bottom) / 2;
            sb.Append($"  <line x1=\"4\" y1=\"{band.Top - 7}\" x2=\"4\" y2=\"{band.Bottom - 7}\" stroke=\"#39445a\" stroke-width=\"2\"/>\\n");
            sb.Append($"  <text x=\"8\" y=\"{midY}\" font-size=\"11\" fill=\"#8b93a5\" font-family=\"sans-serif\" text-anchor=\"middle\" transform=\"rotate(-90 8 {midY})\">{Escape(band.Name)}</text>\\n");
        }
        // edges first (under the boxes); marker per category color
        var catColor = new Dictionary<int, string>();
        var palette = new[] { "#41d3ff", "#6ee7a0", "#f5a05a", "#a78bfa", "#f472b6", "#facc15", "#38bdf8", "#fb7185", "#4ade80", "#e879f9", "#fdba74", "#93c5fd", "#a3e635", "#22d3ee" };
        var pi = 0;
        foreach (var cat in catIds) catColor[cat] = palette[pi++ % palette.Length];
        foreach (var (cat, color) in catColor)
            sb.Append($"  <marker id=\"arr{cat}\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"6\" markerHeight=\"6\" orient=\"auto-start-reverse\"><path d=\"M 0 0 L 10 5 L 0 10 z\" fill=\"{color}\"/></marker>\\n");
        foreach (var e in edges.OrderBy(e => e.From).ThenBy(e => e.To))
        {
            if (!pos.TryGetValue(e.From, out var p1) || !pos.TryGetValue(e.To, out var p2)) continue;
            var from = byId[e.From];
            var x1 = p1.X;
            var y1 = p1.Y + H / 2.0;
            var x2 = p2.X + W;
            var y2 = p2.Y + H / 2.0;
            var mx = (x1 + x2) / 2;
            var color = catColor[from.Category];
            var tip = $"{Escape(from.Name)} requires {Escape(byId[e.To].Name)} at level {e.Lvl}";
            sb.Append($"  <path d=\"M {Fx(x1)} {Fx(y1)} C {Fx(mx)} {Fx(y1)}, {Fx(mx)} {Fx(y2)}, {Fx(x2 - 2)} {Fx(y2)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"1.1\" stroke-opacity=\"0.4\" marker-end=\"url(#arr{from.Category})\"><title>{tip}</title></path>\\n");
        }
        // nodes
        foreach (var n in nodes.OrderBy(n => pos[n.Id].Y).ThenBy(n => pos[n.Id].X))
        {
            var p = pos[n.Id];
            var color = catColor[n.Category];
            sb.Append($"  <g><title>{Escape(n.Name)} (rank {n.Rank}, {Escape(cats[n.Category])})</title>");
            sb.Append($"<rect x=\"{p.X}\" y=\"{p.Y}\" width=\"{W}\" height=\"{H}\" rx=\"6\" fill=\"#1a2233\" stroke=\"{color}\" stroke-width=\"1.3\"/>");
            var label = n.Name.Length > 26 ? n.Name.Substring(0, 25) + "…" : n.Name;
            sb.Append($"<text x=\"{p.X + 8}\" y=\"{p.Y + 19}\" font-size=\"11.5\" fill=\"#d5dbe8\" font-family=\"sans-serif\">{Escape(label)}</text>");
            sb.Append("</g>\\n");
        }
        sb.Append("</svg>\n");
        return (sb.ToString(), nodes.Count, edges.Count);
    }

    private static string Fx(double v) => Math.Round(v, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
