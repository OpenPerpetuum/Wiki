namespace Perpetuum.WikiGenerate;

/// <summary>
/// One wiki page per zone (content/zones/&lt;zone_name&gt;.md). The three worked
/// examples (zone_TM, zone_ASI, zone_gamma_z106) are hand-written and skipped
/// here; everything else gets a generated page with the facts the server
/// records for the zone: type, protection level, size, fertility, plant
/// species, and the ore configuration (with a steady-state stock pie). The
/// zone's teleports are on its map; the inter-zone links, on the world map.
/// </summary>
public static class ZonePages
{
    // Hand-written zone pages the generator must never overwrite.
    private static readonly HashSet<string> HandWritten = new(StringComparer.Ordinal)
    {
        "zone_tm.md", "zone_asi.md", "zone_gamma_z106.md",
    };

    private sealed record Z(string Name, int Id, int Type, int W, int H, bool Protected, bool Terra,
        int Fertility, int Ruleset, string? Note, long? TimeLimit, int? PbsTechLimit, int MaxDock, int SparkCost);

    /// <summary>Generated zone pages: file name (relative to content/zones/) -> content.</summary>
    public static Dictionary<string, string> Build(Db db) => BuildAll(db).Pages;

    /// <summary>Zone pages plus their teleport-map SVGs (static/zonemaps/).</summary>
    public static (Dictionary<string, string> Pages, Dictionary<string, string> Maps) BuildAll(Db db)
    {
        var zones = db.Query("""
            SELECT id, name, zonetype, width, height, protected, terraformable, fertility,
                   plantruleset, note, timeLimitMinutes, pbsTechLimit, maxdockingbase, sparkcost
            FROM zones ORDER BY id
            """)
            .Where(r => r.Int("id") < 49000) // skip the 50000/51000 sentinel duplicates
            .GroupBy(r => r.Str("name"))
            .ToDictionary(g => g.Key, g => ToZone(g.First()));

        var species = db.Query("SELECT rulesetid, COUNT(*) AS n FROM plantrules GROUP BY rulesetid")
            .ToDictionary(r => r.Int("rulesetid"), r => r.Int("n"));

        var minerals = db.Query("SELECT idx, name FROM minerals ORDER BY idx")
            .ToDictionary(r => r.Int("idx"), r => r.Str("name"));
        var configs = db.Query("""
            SELECT zoneid, materialtype, maxnodes, maxtilespernode, totalamountpernode, minthreshold
            FROM mineralconfigs
            """)
            .Select(r => (ZoneId: r.Int("zoneid"), Mat: r.Int("materialtype"), Nodes: r.Int("maxnodes"),
                Tiles: r.Int("maxtilespernode"), Total: r.Lng("totalamountpernode"), Min: r.Dbl("minthreshold")))
            .ToList();

        // The zone pages no longer carry an inter-zone "Connections" section
        // (the zone map shows the TPs, the world map shows the inter-zone
        // links), so the teleportdescriptions/strongholdexitconfig queries are gone.
        var maps = ZoneMapSvg.Build(db);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, z) in zones.OrderBy(kv => kv.Value.Id))
        {
            var file = name.ToLowerInvariant() + ".md";
            if (HandWritten.Contains(file)) continue;
            result[file] = Page(name, z, species, minerals, configs, zones.Count, maps);
        }
        return (result, maps);
    }

    private static Z ToZone(Dictionary<string, object?> r) => new Z(
        r.Str("name"), r.Int("id"), r.Int("zonetype"), r.Int("width"), r.Int("height"),
        r.Bit("protected"), r.Bit("terraformable"), r.Int("fertility"), r.Int("plantruleset"),
        r.TryGetValue("note", out var n) && n is not null ? Convert.ToString(n)! : null,
        r.TryGetValue("timeLimitMinutes", out var t) && t is not null ? Convert.ToInt64(t) : (long?)null,
        r.TryGetValue("pbsTechLimit", out var p) && p is not null ? Convert.ToInt32(p) : (int?)null,
        r.Int("maxdockingbase"), r.Int("sparkcost"));

    private static string Page(
        string name, Z z,
        Dictionary<int, int> species, Dictionary<int, string> minerals,
        List<(int ZoneId, int Mat, int Nodes, int Tiles, long Total, double Min)> configs,
        int totalZones,
        Dictionary<string, string> maps)
    {
        var display = Md.ClientStrings.TryGetValue(name, out var d) && d != name ? d : null;
        var title = display is null ? name : $"{display} ({name})";
        var type = TypeLabel(z.Type);
        var protection = z.Protected ? "alpha" : z.Terra ? "gamma" : "beta";
        var myConfigs = configs.Where(c => c.ZoneId == z.Id).ToList();
        var oreCount = myConfigs.Count;

        // The family tag (alpha/beta/gamma) lets the nav highlight the matching
        // family-listing entry while the reader is on any zone page of the family.
        var sb = new StringBuilder();
        sb.Append(Md.Header(title, $"{type} {ProWord(protection)} zone: " +
            $"{(oreCount > 0 ? oreCount + " ore types, " : "")}" +
            $"{(species.TryGetValue(z.Ruleset, out var sp) ? sp + " plant species" : "no plant rules")}, {z.W}×{z.H} tiles.",
            "zones, mineralconfigs, plantrules, teleportdescriptions, strongholdexitconfig",
            name == "zone_training" ? null : $"family: {protection}")
        );
        sb.Append($"\n# {title}\n\n");

        // Teleport map first: the zone's own extent with its TP columns/landing
        // spots plotted, right under the title (before the facts table and text).
        if (maps.TryGetValue(name, out var svg))
        {
            var slug = name.ToLowerInvariant().Replace("_", "-");
            // A small horizontal card above the map linking to the world map
            // (the zone's place in the galaxy + the inter-zone connections).
            sb.Append("<div class=\"worldmap-card\">\n");
            sb.Append("  <a href=\"/zones/map/\">\n");
            sb.Append("    <img src=\"/world-map-thumb.png\" alt=\"\" loading=\"lazy\">\n");
            sb.Append("    <span class=\"worldmap-card-body\"><strong>World map</strong> — where this zone sits among all the others, with the teleport connections. Open the full map.</span>\n");
            sb.Append("  </a>\n");
            sb.Append("</div>\n\n");
            sb.Append($"![Teleport columns in {title}](/zonemaps/{slug}.svg)\n\n");
            sb.Append(MapCaption(svg.Contains("ltp-line")));
        }

        // Intro line: what kind of zone this is.
        sb.Append(Intro(name, z, display, type, protection, oreCount));
        sb.Append("\n");

        // Facts table.
        sb.Append("\n| Fact | Value |\n|---|---|\n");
        sb.Append($"| Zone id | {z.Id} |\n");
        sb.Append($"| Type | {type} |\n");
        sb.Append($"| Protection | [{Cap(protection)}](/zones/protection/) — {ProDetail(protection)} |\n");
        sb.Append($"| Size | {z.W}×{z.H} tiles |\n");
        if (z.Note is not null && z.Note.StartsWith("gamma tier", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(z.Note["gamma tier ".Length..].Trim(), out var tier))
            sb.Append($"| Tier | T{tier} (server-recorded) |\n");
        sb.Append($"| Fertility | {z.Fertility} |\n");
        sb.Append($"| Plant species | {(species.TryGetValue(z.Ruleset, out var sp2) ? sp2 + " (rule set " + z.Ruleset + ")" : "– (no ruleset)")} |\n");
        if (oreCount > 0)
        {
            var nodes = myConfigs.Sum(c => c.Nodes);
            sb.Append($"| Ore types | {oreCount} |\n");
            sb.Append($"| Total ore nodes | {nodes} |\n");
        }
        if (z.TimeLimit is not null)
            sb.Append($"| Round time limit | {Md.Num((double)z.TimeLimit.Value)} min |\n");
        if (z.PbsTechLimit is not null)
            sb.Append($"| PBS tech limit | {z.PbsTechLimit} |\n");
        var maxDock = z.MaxDock == 0 ? "none" : Md.Num(z.MaxDock);
        sb.Append($"| Max docking bases | {maxDock} |\n");

        // Ore configuration.
        if (oreCount > 0)
        {
            sb.Append("\n## Ore configuration\n\n");
            sb.Append("The node generation this zone maintains. Per-ore yields and the generation formulas " +
                      "are in [Ores](/content/ores/) (this zone's section is linked below).\n\n");
            sb.Append("| Material | Nodes | Max tiles/node | Total per node | Min threshold |\n");
            sb.Append("|---|---|---|---|---|\n");
            foreach (var c in myConfigs.OrderBy(c => c.Mat))
            {
                var ore = minerals.TryGetValue(c.Mat, out var on) ? on : c.Mat.ToString();
                sb.Append($"| [{ore}](/content/ores/{Md.Slug(ore)}/) | {c.Nodes} | {Md.Num(c.Tiles)} | " +
                          $"{Md.Num(c.Total)} | {Md.Cell(c.Min)} |\n");
            }
            sb.Append("\n");
            sb.Append(StockPie(myConfigs, minerals));
        }

        // The inter-zone "Connections" section was removed: the zone map above
        // already shows every TP column/landing point in the zone, and the
        // world map shows the inter-zone teleport links.
        sb.Append($"[Zone index](/zones/zone-index/) · [World map](/zones/map/{MapAnchor(name, z)}) · [Protection levels](/zones/protection/)\n");
        return sb.ToString();
    }

    /// <summary>hasLocal: the zone's map SVG carries data-ltp markers (ZoneMapSvg)
    /// — then the caption explains the hover lines.</summary>
    private static string MapCaption(bool hasLocal)
    {
        return "Where this zone's teleport columns stand (dots, labelled with the destination — dimmed where " +
               "the column is currently switched off), the landing spots of teleports arriving from other " +
               "zones (dashed circles), and the exit gates (diamonds) where one exists. Positions are the " +
               "tile coordinates the server records." +
               (hasLocal
                   ? " The dashed lines connect the pairs of local (in-zone) teleport columns."
                   : "") +
               "\n\n";
    }

    private static string Intro(string name, Z z, string? display, string type, string protection, int oreCount)
    {
        var fam = Family(name);
        var intro = (type, protection) switch
        {
            (_, "alpha") when name == "zone_training" => "The virtual training island — where new characters start and learn the basics.",
            (_, "alpha") when name == "zone_pvp_arena" => "The PvP arena — a scripted combat event zone.",
            (_, "alpha") when name.StartsWith("zone_strghld", StringComparison.Ordinal) => "A stronghold — a protected instance zone with its own exit gate.",
            (_, "alpha") => $"A protected ({Cap(protection)}) {fam} zone — safe from player attack, the standard gathering ground for the {fam} galaxy.",
            (_, "beta") when name.Contains("tc", StringComparison.OrdinalIgnoreCase) => "A small transit zone of the frontier belt — a pure travel node with no ore configuration.",
            (_, "beta") => $"An open-PvP ({protection}) {fam} island — players can attack freely and blobs apply.",
            (_, "gamma") => $"An open-PvP, terraformable ({protection}) {fam} island — the ground can be reshaped by players.",
            _ => $"A {type} zone of the {fam} area.",
        };
        var extra = oreCount > 0
            ? $" It maintains {oreCount} ore type{(oreCount > 1 ? "s" : "")}."
            : "";
        return intro + extra;
    }

    private static string StockPie(List<(int ZoneId, int Mat, int Nodes, int Tiles, long Total, double Min)> myConfigs, Dictionary<int, string> minerals)
    {
        var parts = myConfigs
            .Select(c => (Ore: minerals.TryGetValue(c.Mat, out var n) ? n : "?",
                Stock: (long)c.Nodes * c.Total))
            .Where(p => p.Stock > 0)
            .ToList();
        if (parts.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        sb.Append("\n```mermaid\npie showData\n");
        sb.Append("    title Steady-state ore stock by type (million units)\n");
        foreach (var p in parts.OrderByDescending(p => p.Stock))
            sb.Append($"    \"{p.Ore}\" : {p.Stock / 1_000_000}\n");
        sb.Append("```\n\n");
        return sb.ToString();
    }

    private static string Family(string name) =>
        name.Contains("gamma", StringComparison.OrdinalIgnoreCase) ? "frontier belt"
        : name.Contains("tm", StringComparison.OrdinalIgnoreCase) ? "New Virginia (TM)"
        : name.Contains("ics", StringComparison.OrdinalIgnoreCase) ? "Attalica (ICS)"
        : name.Contains("asi", StringComparison.OrdinalIgnoreCase) ? "Daoden (ASI)"
        : "special";

    private static string TypeLabel(int t) => t switch
    {
        1 => "PvE",
        2 => "PvP",
        3 => "Training",
        4 => "Stronghold",
        _ => "Undefined",
    };

    private static string ProWord(string p) => p switch
    {
        "alpha" => "protected",
        "beta" => "open",
        _ => "open-terraformable",
    };

    private static string ProDetail(string p) => p switch
    {
        "alpha" => "protected, PvP disabled",
        "beta" => "open PvP, standard terrain",
        _ => "open PvP, terraformable",
    };

    private static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Link to a zone's own page (every zone has one).</summary>
    private static string ZoneLink(string name)
    {
        var label = Md.ClientStrings.TryGetValue(name, out var d) && d != name ? d : name;
        return $"[{label}](/zones/{name.ToLowerInvariant().Replace("_", "-")}/)";
    }

    /// <summary>The map section a zone is listed in (for the back-link).</summary>
    private static string MapAnchor(string name, Z z)
    {
        if (name == "zone_training") return "#training";
        if (name is "zone_TM" or "zone_ICS" or "zone_ASI"
            or "zone_TM_pve" or "zone_ICS_pve" or "zone_ASI_pve") return "#alpha";
        if (name.Contains("gamma", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("tc", StringComparison.OrdinalIgnoreCase)) return "#t0";
            if (z.Note is not null && z.Note.StartsWith("gamma tier", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(z.Note["gamma tier ".Length..].Trim(), out var t) && t is >= 1 and <= 3)
                return "#t" + t;
            return "#t1";
        }
        return "#beta"; // open islands, the arena and the strongholds all list under Beta
    }
}
