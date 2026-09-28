namespace Perpetuum.WikiGenerate;

public static class OresPage
{
    public static string Build(Db db, Dictionary<int, DefRow> defs)
    {
        var minerals = db.Query("SELECT idx, name, definition, amount, extractionType, enablereffectrequired, geoscandocument FROM minerals ORDER BY idx")
            .Select(r => new MineralRow(
                r.Int("idx"), r.Str("name"), r.Int("definition"), r.Int("amount"), r.Int("extractionType"),
                r.Bit("enablereffectrequired"),
                r.TryGetValue("geoscandocument", out var g) && g is not null ? Convert.ToInt32(g!) : null))
            .ToList();

        var configs = db.Query("""
            SELECT zoneid, materialtype, maxnodes, maxtilespernode, totalamountpernode, minthreshold
            FROM mineralconfigs ORDER BY zoneid, materialtype
            """)
            .Select(r => new MineralConfigRow(r.Int("zoneid"), r.Int("materialtype"), r.Int("maxnodes"), r.Int("maxtilespernode"), r.Int("totalamountpernode"), r.Dbl("minthreshold")))
            .ToList();

        // id -> name, first (lowest id) row per name; the 50000/51000 sentinel
        // duplicates are skipped.
        var zoneNames = db.Query("SELECT id, name FROM zones ORDER BY id")
            .Where(r => r.Int("id") < 49000)
            .GroupBy(r => r.Int("id"))
            .ToDictionary(g => g.Key, g => g.First().Str("name"));

        var sb = new StringBuilder();
        sb.Append(Md.Header("Ores", "All ore types, their extraction yields, and per-zone node generation parameters.",
            "minerals, mineralconfigs, zones (joined to entitydefaults for ore item names)"));
        sb.Append("\n\n# Ores\n\n");
        sb.Append("Ore exists in the ground as **deposits (nodes)**. Each node holds a limited total amount; " +
                  "when a node is mined below its threshold it is removed and the zone regenerates new nodes " +
                  "up to its configured maximum. How each column is used by the server, with the node-generation " +
                  "formulas, is in [Ore fields](/formats/ore-fields/).\n\n");

        sb.Append("## Ore types\n\n");
        sb.Append("Jump to a type: ");
        for (int i = 0; i < minerals.Count; i++)
        {
            if (i > 0) sb.Append(" · ");
            var m0 = minerals[i];
            sb.Append($"[{m0.Name}](#{Md.Slug(m0.Name)})");
        }
        sb.Append("\n\n");

        // Raw HTML table: markdown tables cannot carry a per-row anchor, and the
        // zone pages link to each ore's row (#titan, #crude, …). The site's table
        // CSS styles plain <table> elements, so this themes like the rest.
        sb.Append("<table>\n");
        sb.Append("<tr><th>Ore</th><th>Item definition</th><th>Amount / extraction</th><th>Extraction type</th><th>Enabler effect</th><th>Geo-scan doc</th></tr>\n");
        foreach (var m in minerals)
        {
            var itemDef = defs.TryGetValue(m.Definition, out var d) ? d.Name : m.Definition.ToString();
            sb.Append($"<tr id=\"{Md.Slug(m.Name)}\"><td><a href=\"#{Md.Slug(m.Name)}\">{m.Name}</a></td>" +
                      $"<td><code>{itemDef}</code></td>" +
                      $"<td>{Md.Num(m.Amount)}</td>" +
                      $"<td>{m.ExtractionType}</td>" +
                      $"<td>{(m.EnablerRequired ? "required" : "no")}</td>" +
                      $"<td>{m.GeoScanDocument?.ToString() ?? "–"}</td></tr>\n");
        }
        sb.Append("</table>\n\n");
        sb.Append("**Extraction type**: 0 = solid (tile-by-tile with a drill), 1 = liquid (continuous pump). " +
                  "**Enabler effect** — when *required*, the robot needs the matching enabler effect active to " +
                  "extract that ore at all.\n\n");

        // Per-zone node generation: one subsection (and table) per zone, in zone
        // id order. Headings are auto-anchored by Zola, so zone pages can deep-link.
        sb.Append("## Per-zone node generation\n\n");
        sb.Append("How many nodes of each ore a zone maintains, and how large each node is — one table per zone. " +
                  "Zones without an ore configuration (the tc transit zones, arenas, strongholds, training) have no section here.\n\n");
        foreach (var g in configs.Where(c => zoneNames.ContainsKey(c.ZoneId))
                     .GroupBy(c => c.ZoneId)
                     .OrderBy(g2 => g2.Key))
        {
            var zoneName = zoneNames[g.Key];
            sb.Append($"### {Md.ZoneName(zoneName)}\n\n");
            var rows = g.OrderBy(c => c.MaterialType)
                .Select(c => new[]
                {
                    OreLink(minerals.FirstOrDefault(m => m.Idx == c.MaterialType)),
                    Md.Num(c.MaxNodes),
                    Md.Num(c.MaxTilesPerNode),
                    Md.Num(c.TotalAmountPerNode),
                    Md.Cell(c.MinThreshold)
                }).ToArray();
            Md.WriteTable(sb, new[] { "Ore", "Max nodes", "Max tiles / node", "Total amount / node", "Min threshold" }, rows);
            sb.Append("\n");
        }

        return sb.ToString();
    }

    /// <summary>Link to an ore's row anchor on this page; plain name when unknown.</summary>
    private static string OreLink(MineralRow? m)
        => m is null ? "" : $"[{m.Name}](#{Md.Slug(m.Name)})";
}
