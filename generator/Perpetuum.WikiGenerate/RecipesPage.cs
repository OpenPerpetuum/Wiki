namespace Perpetuum.WikiGenerate;

public static class RecipesPage
{
    public static string Build(Db db)
    {
        var defs = db.Query("SELECT definition, definitionname FROM entitydefaults")
            .ToDictionary(r => r.Int("definition"), r => r.Str("definitionname"));

        var comps = db.Query("SELECT definition, componentdefinition, componentamount FROM components").ToList();
        var grouped = comps
            .GroupBy(c => c.Int("definition"))
            .Select(g => new
            {
                Def = g.Key,
                Parts = string.Join(", ", g
                    .Select(c => $"{(defs.TryGetValue(c.Int("componentdefinition"), out var dn) ? dn : c.Int("componentdefinition").ToString())} ×{c.Int("componentamount")}")
                    .OrderBy(x => x))
            })
            .OrderBy(g => g.Def)
            .ToList();

        var research = db.Query("SELECT definition, MAX(researchlevel) AS lvl FROM itemresearchlevels GROUP BY definition")
            .ToDictionary(r => r.Int("definition"), r => r.Int("lvl"));

        var sb = new StringBuilder();
        sb.Append(Md.Header("Recipes", "Every craftable item and the components it requires, plus its research level.",
            "components, itemresearchlevels (joined to entitydefaults)"));
        sb.Append("\n\n# Recipes\n\n");
        sb.Append("Component requirements for every item that is assembled from other items (see " +
                  "[Production](/features/production/) in the features section). **Research level** is the maximum research " +
                  "level the item has — higher levels change yield/calibration, not the component list.\n\n");

        var rows = grouped.Select(g => new[]
        {
            defs.TryGetValue(g.Def, out var d) ? d : g.Def.ToString(),
            g.Parts,
            research.TryGetValue(g.Def, out var rl) ? rl.ToString() : "–"
        }).ToArray();
        Md.WriteTable(sb, new[] { "Item", "Components", "Research level" }, rows);

        return sb.ToString();
    }
}
