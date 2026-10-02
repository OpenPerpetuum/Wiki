namespace Perpetuum.WikiGenerate;

public static class ExtensionsPage
{
    public static string Build(Db db, string treeSvg = null, int treeNodes = 0, int treeEdges = 0,
        string categoriesSvg = null, int categoryCount = 0, int categoryEdges = 0)
    {
        var categories = db.Query("SELECT extensioncategoryid, categoryname FROM extensioncategories")
            .ToDictionary(r => r.Int("extensioncategoryid"), r => r.Str("categoryname"));
        var extNames = db.Query("SELECT extensionid, extensionname FROM extensions")
            .ToDictionary(r => r.Int("extensionid"), r => r.Str("extensionname"));
        var prereqs = db.Query("SELECT extensionid, requiredextension, requiredlevel FROM extensionprerequire")
            .GroupBy(r => r.Int("extensionid"))
            .ToDictionary(
                g => g.Key,
                g => string.Join("; ", g.Select(p => $"{extNames.GetValueOrDefault(p.Int("requiredextension"), p.Int("requiredextension").ToString())} ≥{p.Int("requiredlevel")}")));

        var exts = db.Query("""
            SELECT e.extensionid, e.extensionname, e.category, e.rank, e.price, e.bonus,
                   e.targetlearningattribute, e.learningattributeprimary, e.learningattributesecondary, e.active, e.hidden
            FROM extensions e ORDER BY e.category, e.rank, e.extensionname
            """).ToList();

        var sb = new StringBuilder();
        sb.Append(Md.Header("Extensions", "The full extension (skill) tree: every extension, its rank, level-1 price, bonus, and prerequisites.",
            "extensions, extensioncategories, extensionprerequire"));
        sb.Append("\n\n# Extensions\n\n");
        sb.Append("Extensions are per-character skills (see [Research](/features/research/) in the features section). " +
                  "**Price** is the level-1 credit cost; higher levels cost EP only. **Prerequisites** list the extensions (and minimum level) that must be learned first.\n\n");
        if (!string.IsNullOrEmpty(categoriesSvg))
        {
            // The marker comment keeps the Python post-processor
            // (tools/gen_extension_categories.py) idempotent over this page.
            sb.Append("<!-- categories:generated -->\n");
            sb.Append("<a id=\"categories\"></a>\n\n");
            sb.Append("## Main categories\n\n");
            sb.Append($"The {categoryCount} categories at a glance instead of the {treeNodes}-node detail tree: " +
                      "one box per category (extension count, entry points without prerequisites, rank range), " +
                      "and an arrow for every cross-category prerequisite (hover an arrow for the exact " +
                      "requirements) — which categories open up which. **Scroll over the diagram to zoom**, " +
                      "drag to pan, and use the ⟲ button to reset.\n\n");
            sb.Append("<div class=\"map-zoom-wrap\">\n");
            sb.Append("<button type=\"button\" class=\"zoommap-reset\" title=\"Reset the zoom\">\u27f2</button>\n");
            sb.Append($"<img class=\"zoommap\" src=\"/extensions-categories.svg\" alt=\"Extension categories: {categoryCount} categories, " +
                      $"{categoryEdges} cross-category prerequisite edges\" loading=\"lazy\">\n");
            sb.Append("</div>\n\n");
        }
        if (!string.IsNullOrEmpty(treeSvg))
        {
            sb.Append("<a id=\"tree\"></a>\n\n");
            sb.Append("## Extension tree\n\n");
            sb.Append($"The whole tree at a glance: one column per rank (left to right), rows grouped by category, and an arrow for every prerequisite (hover an arrow for the required level). **Scroll over the diagram to zoom**, drag to pan, and use the ⟲ button to reset.\n\n");
            sb.Append("<div class=\"map-zoom-wrap\">\n");
            sb.Append("<button type=\"button\" class=\"zoommap-reset\" title=\"Reset the zoom\">\u27f2</button>\n");
            sb.Append($"<img class=\"zoommap\" src=\"/extensions-tree.svg\" alt=\"Extension tree: {treeNodes} extensions, {treeEdges} prerequisite edges\" loading=\"lazy\">\n");
            sb.Append("</div>\n\n");
        }

        var rows = exts.Select(r => new[]
        {
            r.Str("extensionname"),
            categories.TryGetValue(r.Int("category"), out var c) ? c : r.Int("category").ToString(),
            r.Int("rank").ToString(),
            r.Int("price").ToString(),
            Md.Cell(r.Dbl("bonus")),
            r.Str("targetlearningattribute"),
            r.Str("learningattributeprimary"),
            r.Str("learningattributesecondary"),
            prereqs.TryGetValue(r.Int("extensionid"), out var p) ? p : "–",
            r.Bit("active") ? (r.Bit("hidden") ? "hidden" : "active") : "inactive"
        }).ToArray();
        Md.WriteTable(sb, new[]
        {
            "Extension", "Category", "Rank", "Level-1 price", "Bonus", "Target attribute", "Primary attribute", "Secondary attribute", "Prerequisites", "State"
        }, rows);

        return sb.ToString();
    }
}
