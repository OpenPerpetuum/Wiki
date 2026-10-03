namespace Perpetuum.WikiGenerate;

/// <summary>
/// The extensions page: the clickable categories overview (the SVG boxes
/// link to the sections below) and one section per category with a card per
/// extension (no single big table).
/// </summary>
public static class ExtensionsPage
{
    public static string Build(Db db, string categoriesSvg = null, int categoryCount = 0, int categoryEdges = 0)
    {
        var categories = db.Query("SELECT extensioncategoryid, categoryname FROM extensioncategories")
            .ToDictionary(r => r.Int("extensioncategoryid"), r => r.Str("categoryname"));
        var extNames = db.Query("SELECT extensionid, extensionname FROM extensions")
            .ToDictionary(r => r.Int("extensionid"), r => r.Str("extensionname"));
        // Display names for the prerequisite text ("Electronics ≥5", not the raw id).
        var prereqs = db.Query("SELECT extensionid, requiredextension, requiredlevel FROM extensionprerequire")
            .GroupBy(r => r.Int("extensionid"))
            .ToDictionary(
                g => g.Key,
                g => string.Join("; ", g
                    .OrderBy(p => Md.DisplayName(extNames.GetValueOrDefault(p.Int("requiredextension"), p.Int("requiredextension").ToString())),
                        StringComparer.OrdinalIgnoreCase)
                    .Select(p => $"{Md.DisplayName(extNames.GetValueOrDefault(p.Int("requiredextension"), p.Int("requiredextension").ToString()))} ≥{p.Int("requiredlevel")}")));

        var exts = db.Query("""
            SELECT e.extensionid, e.extensionname, e.category, e.rank, e.price, e.bonus,
                   e.active, e.hidden
            FROM extensions e ORDER BY e.category, e.rank, e.extensionname
            """).ToList();

        var sb = new StringBuilder();
        sb.Append(Md.Header("Extensions", "Every extension (character skill): the clickable category overview and one card per extension by category.",
            "extensions, extensioncategories, extensionprerequire"));
        sb.Append("\n\n# Extensions\n\n");
        sb.Append("Extensions are the per-character skill tree — learned with credits (level 1) and EP (later " +
                  "levels), gated by prerequisites. See [Character](/features/character/) for the character window " +
                  "this tree lives in. The spark extensions (the passive specialization lines) have their own " +
                  "diagram on the [Sparks](/features/sparks/) page.\n\n");
        if (!string.IsNullOrEmpty(categoriesSvg))
        {
            // The marker comment keeps the Python post-processor
            // (tools/gen_extension_categories.py) idempotent over this page.
            sb.Append("<!-- categories:generated -->\n");
            sb.Append("<a id=\"categories\"></a>\n\n");
            sb.Append("## Main categories\n\n");
            sb.Append($"The {categoryCount} categories at a glance: one box per category (extension count, " +
                      "entry points without prerequisites, rank range), left to right by starting rank, and an " +
                      "arrow for every cross-category prerequisite (hover an arrow for the exact requirements) — " +
                      "which categories open up which. **Click a box to jump to that category's cards below.** " +
                      "The spark extensions sit outside this diagram (no prerequisites of their own) — their " +
                      "bundle diagram is on the [Sparks](/features/sparks/) page. **Scroll over the diagram to " +
                      "zoom**, drag to pan, and use the ⟲ button to reset.\n\n");
            sb.Append("<div class=\"map-zoom-wrap extcats-wrap\">\n");
            sb.Append("<button type=\"button\" class=\"zoommap-reset\" title=\"Reset the zoom\">\u27f2</button>\n");
            // The SVG is embedded inline (not via <img>) so its category boxes
            // are real links to the sections below. The zoommap class gives it
            // the same sizing/pan/zoom affordances as the <img> version used
            // before inlining (static/map.js picks up any svg in the wrap).
            var tag = categoriesSvg.IndexOf("<svg ", StringComparison.Ordinal);
            sb.Append(categoriesSvg.Insert(tag + 5, "class=\"zoommap\" "));
            sb.Append("</div>\n\n");
        }

        // One section per category, one card per extension.
        sb.Append("<a id=\"table\"></a>\n\n");
        sb.Append("## Extensions by category\n\n");
        sb.Append("One section per category, one card per extension. **Price** is the level-1 credit cost (later " +
                  "levels cost EP only). **Prerequisites** are the extensions (and minimum level) to learn first. " +
                  "Inactive entries are not offered by the current server build.\n");
        var writtenCats = new HashSet<int>();
        foreach (var ext in exts)
        {
            var catId = ext.Int("category");
            if (!writtenCats.Contains(catId))
            {
                writtenCats.Add(catId);
                var catName = categories.TryGetValue(catId, out var c) ? c : catId.ToString();
                var catCount = exts.Count(x => x.Int("category") == catId);
                sb.Append($"<a id=\"cat-{SlugCat(catName)}\"></a>\n\n");
                sb.Append($"### {Md.DisplayName(catName)} ({catCount})\n\n");
                sb.Append("<div class=\"ext-cards\">\n");
            }
            var name = ext.Str("extensionname");
            var rank = ext.Int("rank");
            var price = Md.Num(ext.Int("price"));
            var bonus = Md.Cell(ext.Dbl("bonus"));
            var active = ext.Bit("active");
            var hidden = ext.Bit("hidden");
            var state = (active, hidden) switch
            {
                (true, false) => "",
                (true, true) => " · hidden",
                _ => " · inactive"
            };
            var prereq = prereqs.TryGetValue(ext.Int("extensionid"), out var p) && p.Length > 0
                ? p
                : "no prerequisites";
            sb.Append("<div class=\"ext-card\">\n");
            sb.Append($"<div class=\"ext-card-name\">{Escape(Md.DisplayName(name))}</div>\n");
            // The interesting values (rank, credits, bonus) are bolded and
            // colored (static/style.css .ext-val-*).
            sb.Append($"<div class=\"ext-card-meta\"><span class=\"ext-val-rank\">rank {rank}</span> · " +
                      $"<span class=\"ext-val-price\">{price} cr</span> · <span class=\"ext-val-bonus\">bonus {Escape(bonus)}</span>{state}</div>\n");
            sb.Append($"<div class=\"ext-card-prereq\">{Escape(prereq)}</div>\n");
            sb.Append("</div>\n");
            if (exts.Last(x => x.Int("category") == catId).Int("extensionid") == ext.Int("extensionid"))
                sb.Append("</div>\n\n");
        }

        return sb.ToString();
    }

    /// <summary>"extcat_craft" → "craft" — the anchor of the category's section
    /// on the extensions page (shared with ExtensionsCategories, which links
    /// the overview boxes to it).</summary>
    public static string SlugCat(string s) => Slug(s.Replace("extcat_", ""));
    private static string Slug(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return sb.ToString().Trim('-').Replace("--", "-");
    }

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
