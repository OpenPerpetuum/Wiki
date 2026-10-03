namespace Perpetuum.WikiGenerate;

/// <summary>
/// The spark family overview graph shown at the top of the sparks page
/// (inlined into content/features/sparks.md by SparksPage.cs): one box per
/// family (label, spark count, unlock type), colored, each wrapping a link
/// to the family's card section further down the page (#family-&lt;slug&gt;).
/// Compact and horizontal on purpose — the vertical spark connection tree
/// (static/sparks-tree.svg) is the detailed view and stays further down.
/// Rendered like the extensions category overview: plain rectangles + text,
/// no images (the extension category art is copyrighted).
/// </summary>
public static class SparksFamilySvg
{
    public static string Build(List<SparkFamilyInfo> families)
    {
        const int mx = 24, my = 40;
        const int bw = 250, bh = 74, gx = 30, gy = 30;
        const int cols = 6;
        var rows = (int)Math.Ceiling(families.Count / (double)cols);
        var w = mx * 2 + cols * bw + (cols - 1) * gx;
        var h = my * 2 + rows * bh + (rows - 1) * gy;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w} {h}\" width=\"1280\" height=\"{(int)(1280 * h / (double)w)}\" role=\"img\" ");
        sb.Append($"aria-label=\"Spark families: {families.Count} families, {families.Sum(f => f.Count)} sparks; click a family to jump to its sparks below\">\n");
        sb.Append("  <rect x=\"0\" y=\"0\" width=\"");
        sb.Append(w.ToString());
        sb.Append("\" height=\"");
        sb.Append(h.ToString());
        sb.Append("\" fill=\"#10151f\" stroke=\"#39445a\" stroke-width=\"1\"/>\n");

        for (var i = 0; i < families.Count; i++)
        {
            var f = families[i];
            var col = i % cols;
            var row = i / cols;
            var x = mx + col * (bw + gx);
            var y = my + row * (bh + gy);
            var tip = $"{f.Label}: {f.Count} sparks, unlocks with {f.Unlock}";
            sb.Append($"  <g><title>{Escape(tip)}</title><a href=\"#family-{f.Slug}\">");
            sb.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"{bw}\" height=\"{bh}\" rx=\"10\" fill=\"#10151f\" stroke=\"{f.Color}\" stroke-width=\"1.5\"/>");
            sb.Append($"<text x=\"{x + bw / 2}\" y=\"{y + 28}\" text-anchor=\"middle\" fill=\"#e8eefc\" font-size=\"15\" font-weight=\"700\">{Escape(f.Label)}</text>");
            sb.Append($"<text x=\"{x + bw / 2}\" y=\"{y + 52}\" text-anchor=\"middle\" fill=\"{f.Color}\" font-size=\"12\">{f.Count} sparks · {Escape(f.Unlock)}</text>");
            sb.Append("</a></g>\n");
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

/// <summary>One family box: display label, link slug (matches the
/// #family-&lt;slug&gt; anchors SparksPage writes), stroke color and the
/// spark count / unlock summary.</summary>
public sealed record SparkFamilyInfo(string Label, string Slug, string Color, int Count, string Unlock);
