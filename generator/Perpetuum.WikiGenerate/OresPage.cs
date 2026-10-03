namespace Perpetuum.WikiGenerate;

public static class OresPage
{
    private sealed record Use(int Recipe, int OreDef, int Amount, string Name);

    /// <summary>
    /// The ores index plus one page per ore type (under ores/&lt;slug&gt;.md):
    /// extraction facts, the zones the ore spawns in (linked) and what is made
    /// from it (linked).
    /// </summary>
    public static (string Index, List<(string File, string Content)> OrePages) Build(Db db, Dictionary<int, DefRow> defs)
    {
        var (minerals, configs, zoneNames, uses) = Load(db);
        var index = IndexBuild(minerals, configs, zoneNames);
        var orePages = OrePagesBuild(db, minerals, configs, zoneNames, uses, defs);
        return (index, orePages);
    }

    private static (List<MineralRow> Minerals, List<MineralConfigRow> Configs,
              Dictionary<int, string> ZoneNames, List<Use> Uses) Load(Db db)
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

        // What is crafted from each ore: components rows whose recipe is a visible
        // catalog item (def_*). Ore identity is the componentdefinition column, so
        // a page can pick out the recipes that use *that* ore.
        var defById = new Dictionary<int, string>();
        foreach (var d in db.Query("SELECT definition, definitionname FROM entitydefaults"))
            defById[d.Int("definition")] = d.Str("definitionname");
        var uses = db.Query("SELECT definition, componentdefinition, componentamount FROM components")
            .Select(r => new Use(r.Int("definition"), r.Int("componentdefinition"), r.Int("componentamount"),
                                 defById.TryGetValue(r.Int("definition"), out var n) ? n : r.Int("definition").ToString()))
            .Where(u => u.Name.StartsWith("def_", StringComparison.Ordinal))
            .ToList();
        return (minerals, configs, zoneNames, uses);
    }

    private static string IndexBuild(List<MineralRow> minerals, List<MineralConfigRow> configs, Dictionary<int, string> zoneNames)
    {
        var sb = new StringBuilder();
        sb.Append(Md.Header("Ores", "All ore types, their extraction yields, and per-zone node generation parameters.",
            "minerals, mineralconfigs, zones (joined to entitydefaults for ore item names)"));
        sb.Append("\n\n# Ores\n\n");
        sb.Append("Ore exists in the ground as **deposits (nodes)**. Each node holds a limited total amount; " +
                  "when a node is mined below its threshold it is removed and the zone regenerates new nodes " +
                  "up to its configured maximum. Every ore has its own page with its extraction facts, the " +
                  "zones it spawns in and what is made from it. How each column is used by the server, with " +
                  "the node-generation formulas, is in [Ore fields](/formats/ore-fields/).\n\n");

        sb.Append("## Ore types\n\n");
        for (var i = 0; i < minerals.Count; i++)
        {
            if (i > 0) sb.Append(" · ");
            var m0 = minerals[i];
            sb.Append($"[{m0.Name}]({OreUrl(m0)})");
        }
        sb.Append("\n\n");

        sb.Append("<table>\n");
        sb.Append("<tr><th>Ore</th><th>Amount / extraction</th><th>Extraction type</th><th>Enabler effect</th><th>Geo-scan doc</th></tr>\n");
        foreach (var m in minerals)
        {
            sb.Append($"<tr><td><a href=\"{OreUrl(m)}\">{m.Name}</a></td>" +
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
                    OrePageLink(minerals.FirstOrDefault(m => m.Idx == c.MaterialType)),
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

    /// <summary>Site URL of an ore's dedicated page.</summary>
    public static string OreUrl(MineralRow m) => "/content/ores/" + Md.Slug(m.Name) + "/";

    /// <summary>Link to an ore's dedicated page; plain name when unknown.</summary>
    private static string OrePageLink(MineralRow? m)
        => m is null ? "" : $"[{m.Name}]({OreUrl(m)})";

    /// <summary>One page per ore type under ores/.</summary>
    private static List<(string File, string Content)> OrePagesBuild(Db db, List<MineralRow> minerals,
        List<MineralConfigRow> configs, Dictionary<int, string> zoneNames, List<Use> uses, Dictionary<int, DefRow> defs)
    {
        var research = db.Query("SELECT definition, MAX(researchlevel) AS lvl FROM itemresearchlevels GROUP BY definition")
            .ToDictionary(r => r.Int("definition"), r => r.Int("lvl"));
        var byName = new Dictionary<string, DefRow>();
        foreach (var (_, v) in defs) byName[v.Name] = v;
        var pages = new List<(string File, string Content)>();
        foreach (var m in minerals)
        {
            // Ores are excluded from the item catalog (CfOre) even when enabled
            // and visible; only link when a catalog page actually exists.
            var itemDef = byName.TryGetValue(m.Name, out var id2) && id2.Enabled && !id2.Hidden && !HasOreFlag(id2) ? m.Name : null;
            var itemUrl = itemDef is null ? null : ItemUrl(itemDef);
            var sb = new StringBuilder();
            var title = Md.DisplayName(m.Name);
            sb.Append(Md.Header(title,
                $"{title}: extraction yield, the zones it spawns in, and what is made from it.",
                "minerals, mineralconfigs, zones, components, itemresearchlevels"));
            sb.Append($"\n# {title}\n\n");
            var kind = m.ExtractionType == 1 ? "liquid (pumped continuously)" : "solid (drilled tile by tile)";
            sb.Append($"**{title}** is a {kind} ore. Each extraction yields **{Md.Num(m.Amount)}** units, " +
                      $"and the zone holds it in finite **deposits (nodes)** — when a node is mined below its " +
                      "threshold it is removed and the zone regenerates nodes up to its configured maximum. " +
                      (m.EnablerRequired
                          ? "Extracting this ore requires the matching enabler effect to be active on the robot. "
                          : "") +
                      "How the node-generation columns below are used by the server is in [Ore fields](/formats/ore-fields/).\n\n");
            sb.Append("## Extraction\n\n");
            var rows = new[]
            {
                new[] { "Amount / extraction", Md.Num(m.Amount) },
                new[] { "Extraction type", kind },
                new[] { "Enabler effect", m.EnablerRequired ? "required" : "not required" },
            };
            if (m.GeoScanDocument is not null)
                rows = rows.Append(new[] { "Geo-scan document", $"definition {m.GeoScanDocument}" }).ToArray();
            Md.WriteTable(sb, new[] { "Property", "Value" }, rows);
            sb.Append("\n");
            if (itemUrl is not null)
                sb.Append($"\nThe material itself is the item [{Md.DisplayName(itemDef!)}]({itemUrl}).\n\n");
            // Where it spawns: every zone config row for this ore, in zone id order.
            var here = configs.Where(c => c.MaterialType == m.Idx).OrderBy(c => c.ZoneId).ToList();
            if (here.Count == 0)
                sb.Append("\nThis ore has no zone node configuration — it is not regenerated in any zone " +
                          "(it exists as a material for production and missions).\n\n");
            if (here.Count > 0)
            {
                sb.Append("## Where it spawns\n\n");
                sb.Append("Zones that regenerate this ore, with their node configuration. Zones without an entry do not hold this ore.\n\n");
                var rows2 = here
                    .Where(c => zoneNames.ContainsKey(c.ZoneId))
                    .Select(c => new[]
                    {
                        $"[{Md.ZoneName(zoneNames[c.ZoneId])}]({ZonePageUrl(zoneNames[c.ZoneId])})",
                        Md.Num(c.MaxNodes),
                        Md.Num(c.MaxTilesPerNode),
                        Md.Num(c.TotalAmountPerNode),
                        Md.Cell(c.MinThreshold)
                    }).ToArray();
                Md.WriteTable(sb, new[] { "Zone", "Max nodes", "Max tiles / node", "Total amount / node", "Min threshold" }, rows2);
                sb.Append("\n");
            }
            // Made from it: items whose recipe uses this ore as a component.
            var made = uses.Where(u => u.OreDef == m.Definition)
                .GroupBy(u => u.Recipe)
                .Select(g => g.First())
                .OrderBy(u => u.Name, StringComparer.Ordinal)
                .ToList();
            if (made.Count > 0)
            {
                sb.Append("## Made from it\n\n");
                sb.Append("Items that take this ore as a recipe component (amount per single production). " +
                          "Full component lists are on each item's page.\n\n");
                var rows3 = made
                    .Select(u => new[]
                    {
                        ItemLink(u.Name, byName),
                        Md.Num(u.Amount),
                        research.TryGetValue(u.Recipe, out var lvl) && lvl > 0 ? Md.Num(lvl) : "–"
                    }).ToArray();
                Md.WriteTable(sb, new[] { "Item", $"Amount of {title}", "Research level" }, rows3);
                sb.Append("\n");
            }
            sb.Append($"\n[All ores](/content/ores/) · [Ore fields](/formats/ore-fields/)\n");
            pages.Add((Path.Combine("ores", Md.Slug(m.Name) + ".md"), sb.ToString()));
        }
        return pages;
    }

    private static string ItemUrl(string defname)
        => "/content/items/" + defname["def_".Length..].ToLowerInvariant().Replace('_', '-') + "/";

    private static bool HasOreFlag(DefRow d) => (d.CatFlags & Flags.CfOre) == Flags.CfOre;

    /// <summary>
    /// Link to an item's page when it is a catalog page, otherwise the display
    /// name alone. The catalog test is ItemsPage.IsItem — robot fits (_bot),
    /// prototypes (_bot_pr), ores, deployables and NPC fits have no item page,
    /// so a bare "known in entitydefaults" check would link to 404s.
    /// </summary>
    private static string ItemLink(string defname, Dictionary<string, DefRow> byName)
        => byName.TryGetValue(defname, out var d) && d.Enabled && !d.Hidden && ItemsPage.IsItem(d)
            ? $"[{Md.DisplayName(defname)}]({ItemUrl(defname)})"
            : Md.DisplayName(defname);

    /// <summary>Site URL of a zone's dedicated page (same slug scheme as ZonePages).</summary>
    private static string ZonePageUrl(string zoneName) => "/zones/" + zoneName.ToLowerInvariant().Replace("_", "-") + "/";
}
