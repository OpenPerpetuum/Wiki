namespace Perpetuum.WikiGenerate;

public static class ExtensionsPage
{
    public static string Build(Db db)
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
