namespace Perpetuum.WikiGenerate;

/// <summary>
/// The spark connection tree (static/sparks-tree.svg) for the sparks page:
/// every spark (grouped by family, left) and the spark extensions it bundles
/// (right), one arrow per bundle entry with the granted level. Source:
/// sparks + sparkextensions + extensions.
/// </summary>
public static class SparksTree
{
    public static (string Svg, int Sparks, int Edges) Build(Db db)
    {
        var sparks = db.Query("""
            SELECT s.id, s.sparkname, s.unlockprice, s.standinglimit, s.definition, s.quantity,
                   s.changeprice, s.displayorder, s.defaultspark, s.hidden, s.alliancename
            FROM sparks s
            ORDER BY s.displayorder, s.sparkname
            """).ToList();
        var extNames = db.Query("SELECT extensionid, extensionname FROM extensions")
            .ToDictionary(r => r.Int("extensionid"), r => r.Str("extensionname"));
        var bundle = db.Query("SELECT sparkid, extensionid, extensionlevel FROM sparkextensions")
            .GroupBy(r => r.Int("sparkid"))
            .ToDictionary(g => g.Key, g => g.Select(r => (Ext: r.Int("extensionid"), Lvl: r.Int("extensionlevel"))).ToList());

        // Family groups, in page order: faction lines, paid syndicate lines,
        // the limited/event specials.
        string Family(string name)
        {
            var t = name.StartsWith("spark_") ? name["spark_".Length..] : name;
            var first = t.Split('_')[0];
            return first switch
            {
                "tm" => "TM (Truhold-Markson)",
                "ics" => "ICS",
                "asi" => "ASI",
                "syndicate" => "Syndicate (NIC)",
                "anniversary" or "amazon" or "steam" => "Limited",
                _ => "Event & special"
            };
        }
        string SparkLabel(string name)
        {
            var t = name.StartsWith("spark_") ? name["spark_".Length..] : name;
            return string.Join(' ', t.Split('_').Select(tok =>
                tok.Length > 3 && tok.StartsWith("lvl", StringComparison.Ordinal) ? $"Lvl{tok[3..]}" :
                char.ToUpperInvariant(tok[0]) + tok[1..]));
        }
        string ExtLabel(string name)
        {
            var d = Md.DisplayName(name);
            return d.StartsWith("Spark ", StringComparison.Ordinal) ? d["Spark ".Length..] : d;
        }

        // Right column: the bundled extensions, ordered by name.
        var extIds = bundle.Values.SelectMany(v => v).Select(b => b.Ext).Distinct().OrderBy(e => ExtLabel(extNames[e]), StringComparer.Ordinal).ToList();

        const int SX = 24, SW = 240, SH = 28, SG = 7, FX = 18, FH = 22, MX = 24, MY = 30;
        const int EX = 0, EW = 200, EH = 28, EG = 7;
        var groups = new List<(string Family, List<int> Sparks)>();
        foreach (var s in sparks)
        {
            var f = Family(s.Str("sparkname"));
            if (groups.Count == 0 || groups[^1].Family != f) groups.Add((f, new List<int>()));
            groups[^1].Sparks.Add(s.Int("id"));
        }

        // Left layout: family header + spark rows.
        var y = MY;
        var posL = new Dictionary<int, int>();
        var familyRects = new List<(string Name, int Y, int H)>();
        foreach (var (family, ids) in groups)
        {
            var gy = y;
            y += FH;
            foreach (var id in ids)
            {
                posL[id] = y;
                y += SH + SG;
            }
            y += 6;
            familyRects.Add((family, gy, y - gy));
        }
        var leftH = y;
        var rightH = extIds.Count * EH + (extIds.Count - 1) * EG;
        var W = SX + SW + FX + EW + MX;
        var H = Math.Max(leftH, MY + rightH + MY) + 4;
        var posR = new Dictionary<int, int>();
        var ry = MY + (H - 2 * MY - rightH) / 2;
        foreach (var id in extIds)
        {
            posR[id] = ry;
            ry += EH + EG;
        }
        var ex = SX + SW + FX;

        string Tooltip(int id)
        {
            var s = sparks.Single(x => x.Int("id") == id);
            var parts = new List<string>();
            if (s.Dbl("unlockprice") > 0) parts.Add($"unlock {Md.Num(s.Int("unlockprice"))} NIC");
            if (s.Dbl("standinglimit") > 0 && !string.IsNullOrEmpty(s.Str("alliancename")))
                parts.Add($"standing {s.Dbl("standinglimit"):0.#} with {s.Str("alliancename")}");
            if (s.Int("definition") > 0) parts.Add($"unlock item ×{s.Int("quantity")}");
            if (parts.Count == 0) parts.Add(s.Bit("defaultspark") ? "default spark (given at character creation)" : "no unlock requirement");
            var cp = s.Int("changeprice");
            if (cp > 0) parts.Add($"switch {Md.Num(cp)} NIC");
            return SparkLabel(s.Str("sparkname")) + " — " + string.Join(", ", parts);
        }

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {W} {H}\" width=\"900\" " +
                  $"height=\"{(int)(900.0 * H / W)}\" role=\"img\" " +
                  $"aria-label=\"Spark connection tree: {sparks.Count} sparks and the {extIds.Count} spark extensions they bundle\">\n");
        sb.Append($"  <rect x=\"0\" y=\"0\" width=\"{W}\" height=\"{H}\" fill=\"#10151f\" stroke=\"#39445a\" stroke-width=\"1\"/>\n");
        sb.Append($"  <text x=\"{SX + SW / 2}\" y=\"{MY - 12}\" font-size=\"13\" fill=\"#8b93a5\" text-anchor=\"middle\" " +
                  "font-family=\"sans-serif\">Sparks</text>\n");
        sb.Append($"  <text x=\"{ex + EW / 2}\" y=\"{MY - 12}\" font-size=\"13\" fill=\"#8b93a5\" text-anchor=\"middle\" " +
                  "font-family=\"sans-serif\">Spark extensions (bundle)</text>\n");
        sb.Append("  <defs><marker id=\"arrs\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"6\" " +
                  "markerHeight=\"6\" orient=\"auto\"><path d=\"M 0 0 L 10 5 L 0 10 z\" fill=\"#54658a\"/></marker></defs>\n");

        // Bundle edges (under the boxes): spark -> extension, level labelled.
        var edgeCount = 0;
        foreach (var s in sparks)
        {
            var id = s.Int("id");
            if (!bundle.TryGetValue(id, out var parts)) continue;
            var sy = posL[id] + SH / 2.0;
            var sx = SX + SW;
            foreach (var (extId, lvl) in parts)
            {
                var ey = posR[extId] + EH / 2.0;
                var mx = (sx + ex) / 2;
                sb.Append($"  <path d=\"M {sx} {sy} C {mx} {sy}, {mx} {ey}, {ex - 2} {ey}\" fill=\"none\" " +
                          "stroke=\"#54658a\" stroke-width=\"1\" stroke-opacity=\"0.5\" marker-end=\"url(#arrs)\">" +
                          $"<title>{Escape(SparkLabel(s.Str("sparkname")))} grants {Escape(ExtLabel(extNames[extId]))} at level {lvl}</title></path>\n");
                sb.Append($"  <text x=\"{mx}\" y=\"{(sy + ey) / 2 - 3}\" font-size=\"10\" fill=\"#5f6b85\" " +
                          $"text-anchor=\"middle\" font-family=\"sans-serif\">{lvl}</text>\n");
                edgeCount++;
            }
        }

        // Family headers + spark boxes (left).
        foreach (var (family, fy, fh) in familyRects)
        {
            sb.Append($"  <text x=\"{SX}\" y=\"{fy + 14}\" font-size=\"12\" font-weight=\"bold\" fill=\"#aab2c5\" " +
                      $"font-family=\"sans-serif\">{Escape(family)}</text>\n");
        }
        foreach (var s in sparks)
        {
            var id = s.Int("id");
            var yy = posL[id];
            sb.Append($"  <g><title>{Escape(Tooltip(id))}</title>");
            sb.Append($"<rect x=\"{SX}\" y=\"{yy}\" width=\"{SW}\" height=\"{SH}\" rx=\"6\" fill=\"#1a2233\" " +
                      "stroke=\"#39445a\" stroke-width=\"1\"/>");
            sb.Append($"<text x=\"{SX + 10}\" y=\"{yy + 18}\" font-size=\"12\" fill=\"#e8ecf4\" " +
                      $"font-family=\"sans-serif\">{Escape(SparkLabel(s.Str("sparkname")))}</text>");
            if (s.Bit("defaultspark"))
                sb.Append($"<text x=\"{SX + SW - 10}\" y=\"{yy + 18}\" font-size=\"10\" fill=\"#8b93a5\" text-anchor=\"end\" font-family=\"sans-serif\">default</text>");
            sb.Append("</g>\n");
        }
        // Extension boxes (right).
        foreach (var id in extIds)
        {
            var yy = posR[id];
            sb.Append($"  <g><title>{Escape(ExtLabel(extNames[id]))} — spark extension granted by the spark bundles</title>");
            sb.Append($"<rect x=\"{ex}\" y=\"{yy}\" width=\"{EW}\" height=\"{EH}\" rx=\"6\" fill=\"#16241f\" " +
                      "stroke=\"#2f9e6f\" stroke-width=\"1.2\"/>");
            sb.Append($"<text x=\"{ex + 10}\" y=\"{yy + 18}\" font-size=\"12\" fill=\"#e8ecf4\" " +
                      $"font-family=\"sans-serif\">{Escape(ExtLabel(extNames[id]))}</text>");
            sb.Append("</g>\n");
        }
        sb.Append("</svg>\n");
        return (sb.ToString(), sparks.Count, edgeCount);
    }

    private static string Escape(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
