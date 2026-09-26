namespace Perpetuum.WikiGenerate;

public static class TechTreePage
{
    public static string Build(Db db)
    {
        var defs = db.Query("SELECT definition, definitionname FROM entitydefaults")
            .ToDictionary(r => r.Int("definition"), r => r.Str("definitionname"));
        var exts = db.Query("SELECT extensionid, extensionname FROM extensions")
            .ToDictionary(r => r.Int("extensionid"), r => r.Str("extensionname"));
        var groups = db.Query("SELECT id, name FROM techtreegroups").ToDictionary(r => r.Int("id"), r => r.Str("name"));
        var pointTypes = db.Query("SELECT id, name FROM techtreepointtypes").ToDictionary(r => r.Int("id"), r => r.Str("name"));

        var prices = db.Query("SELECT definition, pointtype, amount FROM techtreenodeprices")
            .GroupBy(r => r.Int("definition"))
            .ToDictionary(g => g.Key, g => string.Join("; ", g
                .Where(p => pointTypes.TryGetValue(p.Int("pointtype"), out var pt))
                .Select(p => $"{pointTypes[p.Int("pointtype")]}={p.Int("amount")}")));

        var nodes = db.Query("""
            SELECT tt.childdefinition, tt.parentdefinition, tt.groupID, tt.enablerextensionid
            FROM techtree tt ORDER BY tt.groupID, tt.childdefinition
            """).ToList();

        var sb = new StringBuilder();
        sb.Append(Md.Header("Tech tree", "Every tech tree node: the item it unlocks, its parent node, group, enabler extension, and point prices.",
            "techtree, techtreegroups, techtreenodeprices, techtreepointtypes (joined to entitydefaults and extensions)"));
        sb.Append("\n\n# Tech tree\n\n");
        sb.Append("Tech tree nodes unlock items by spending **research points** (earned from kernels — see " +
                  "[Research](/features/research/) in the features section). A node can only be unlocked when its " +
                  "**parent** is unlocked and the **enabler extension** is learned. Points: " +
                  string.Join(", ", pointTypes.Values) + ".\n\n");

        var rows = nodes.Select(n => new[]
        {
            defs.TryGetValue(n.Int("childdefinition"), out var c) ? c : n.Int("childdefinition").ToString(),
            defs.TryGetValue(n.Int("parentdefinition"), out var p) ? p : "–",
            groups.TryGetValue(n.Int("groupID"), out var g) ? g : n.Int("groupID").ToString(),
            exts.TryGetValue(n.Int("enablerextensionid"), out var e) ? e : "–",
            prices.TryGetValue(n.Int("childdefinition"), out var pr) ? pr : "free"
        }).ToArray();
        Md.WriteTable(sb, new[] { "Unlocks item", "Parent node", "Group", "Enabler extension", "Point prices" }, rows);

        return sb.ToString();
    }
}
