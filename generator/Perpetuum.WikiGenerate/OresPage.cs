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

        var configs = db.Query("SELECT zoneid, materialtype, maxnodes, maxtilespernode, totalamountpernode, minthreshold FROM mineralconfigs ORDER BY zoneid, materialtype")
            .Select(r => new MineralConfigRow(r.Int("zoneid"), r.Int("materialtype"), r.Int("maxnodes"), r.Int("maxtilespernode"), r.Int("totalamountpernode"), r.Dbl("minthreshold")))
            .ToList();

        var zones = db.Query("SELECT id, name, enabled FROM zones ORDER BY id").ToDictionary(r => r.Int("id"), r => r.Str("name"));

        var sb = new StringBuilder();
        sb.Append(Md.Header("Ores", "All ore types, their extraction yields, and per-zone node generation parameters.",
            "minerals, mineralconfigs, zones (joined to entitydefaults for ore item names)"));
        sb.Append("\n\n# Ores\n\n");
        sb.Append("Ore exists in the ground as **deposits (nodes)**. Each node holds a limited total amount; when a node is mined below its threshold it is removed and the zone regenerates new nodes up to its configured maximum. `materialtype` in the config maps 1:1 to the ore `idx` above (0 = undefined). How each column is used by the server, with the node-generation formulas, is in [Ore fields](/formats/ore-fields/).\n\n");

        sb.Append("## Ore types\n\n");
        var oreRows = minerals.Select(m => new[]
        {
            m.Name,
            defs.TryGetValue(m.Definition, out var d) ? d.Name : m.Definition.ToString(),
            Md.Num(m.Amount),
            m.ExtractionType.ToString(),
            m.EnablerRequired ? "yes" : "no",
            m.GeoScanDocument?.ToString() ?? "–"
        }).ToArray();
        Md.WriteTable(sb, new[] { "Ore", "Item definition", "Amount / extraction", "Extraction type", "Enabler effect required", "Geo-scan doc" }, oreRows);
        sb.Append('\n');

        sb.Append("## Per-zone node generation\n\n");
        sb.Append("How many nodes of each ore a zone maintains, and how large each node is.\n\n");
        var cfgRows = configs.Where(c => zones.ContainsKey(c.ZoneId)).Select(c => new[]
        {
            Md.ZoneName(zones[c.ZoneId]),
            minerals.FirstOrDefault(m => m.Idx == c.MaterialType)?.Name ?? c.MaterialType.ToString(),
            Md.Num(c.MaxNodes),
            Md.Num(c.MaxTilesPerNode),
            Md.Num(c.TotalAmountPerNode),
            Md.Cell(c.MinThreshold)
        }).ToArray();
        if (cfgRows.Length > 0)
            Md.WriteTable(sb, new[] { "Zone", "Ore", "Max nodes", "Max tiles / node", "Total amount / node", "Min threshold" }, cfgRows);
        else
            sb.Append("_No mineralconfig rows._\n");

        return sb.ToString();
    }
}
