namespace Perpetuum.WikiGenerate;

/// <summary>
/// One wiki page per zone (content/zones/&lt;zone_name&gt;.md). The three worked
/// examples (zone_TM, zone_ASI, zone_gamma_z106) are hand-written and skipped
/// here; everything else gets a generated page with the facts the server
/// records for the zone: type, protection level, size, fertility, plant
/// species, the ore configuration (with a steady-state stock pie), and the
/// inter-zone teleport connections.
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

        var tps = db.Query("""
            SELECT zs.name AS src, zd.name AS dst, COUNT(*) AS tps
            FROM teleportdescriptions td
            JOIN zones zs ON zs.id = td.sourcezone
            JOIN zones zd ON zd.id = td.targetzone
            WHERE td.sourcezone <> td.targetzone AND td.active = 1
            GROUP BY zs.name, zd.name
            """)
            .Select(r => (Src: r.Str("src"), Dst: r.Str("dst"), Tps: r.Int("tps")))
            .ToList();

        var exits = db.Query("""
            SELECT sz.name AS src, dz.name AS dst, rc.name AS rift
            FROM strongholdexitconfig se
            JOIN zones sz ON sz.id = se.zoneid
            JOIN riftconfigs rc ON rc.id = se.riftConfigId
            JOIN riftdestinations rd ON rd.groupId = rc.destinationGroupId
            JOIN zones dz ON dz.id = rd.zoneId
            """)
            .Select(r => (Src: r.Str("src"), Dst: r.Str("dst"), Rift: r.Str("rift")))
            .GroupBy(l => (l.Src, l.Dst))
            .Select(g => g.First())
            .ToList();

        var maps = ZoneMapSvg.Build(db);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, z) in zones.OrderBy(kv => kv.Value.Id))
        {
            var file = name.ToLowerInvariant() + ".md";
            if (HandWritten.Contains(file)) continue;
            result[file] = Page(name, z, species, minerals, configs, tps, exits, zones.Count, maps);
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
        List<(string Src, string Dst, int Tps)> tps,
        List<(string Src, string Dst, string Rift)> exits, int totalZones,
        Dictionary<string, string> maps)
    {
        var display = Md.ClientStrings.TryGetValue(name, out var d) && d != name ? d : null;
        var title = display is null ? name : $"{display} ({name})";
        var type = TypeLabel(z.Type);
        var protection = z.Protected ? "alpha" : z.Terra ? "gamma" : "beta";
        var myConfigs = configs.Where(c => c.ZoneId == z.Id).ToList();
        var oreCount = myConfigs.Count;

        var sb = new StringBuilder();
        sb.Append(Md.Header(title, $"{type} {ProWord(protection)} zone: " +
            $"{(oreCount > 0 ? oreCount + " ore types, " : "")}" +
            $"{(species.TryGetValue(z.Ruleset, out var sp) ? sp + " plant species" : "no plant rules")}, {z.W}×{z.H} tiles.",
            "zones, mineralconfigs, plantrules, teleportdescriptions, strongholdexitconfig"));
        sb.Append($"\n# {title}\n\n");

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
                sb.Append($"| [{ore}](/content/ores/#{Md.Slug(ore)}) | {c.Nodes} | {Md.Num(c.Tiles)} | " +
                          $"{Md.Num(c.Total)} | {Md.Cell(c.Min)} |\n");
            }
            sb.Append("\n");
            sb.Append(StockPie(myConfigs, minerals));
        }

        // Connections.
        var outTps = tps.Where(t => t.Src == name).ToList();
        var inTps = tps.Where(t => t.Dst == name).ToList();
        var myExits = exits.Where(e => e.Src == name).ToList();
        if (outTps.Count > 0 || inTps.Count > 0 || myExits.Count > 0)
        {
            sb.Append("\n## Connections\n\n");
            if (outTps.Count > 0)
            {
                sb.Append("**Teleports out** (TP columns from this zone):\n\n");
                foreach (var t in outTps.OrderBy(t => t.Dst, StringComparer.Ordinal))
                    sb.Append($"- → {ZoneLink(t.Dst)} ({t.Tps} TP point{(t.Tps > 1 ? "s" : "")})\n");
                sb.Append("\n");
            }
            if (inTps.Count > 0)
            {
                sb.Append("**Teleports in** (other zones with a TP column to this one):\n\n");
                foreach (var t in inTps.OrderBy(t => t.Src, StringComparer.Ordinal))
                    sb.Append($"- ← {ZoneLink(t.Src)} ({t.Tps} TP point{(t.Tps > 1 ? "s" : "")})\n");
                sb.Append("\n");
            }
            if (myExits.Count > 0)
            {
                sb.Append("**Exit gates** (stronghold/arena exits recorded in the database):\n\n");
                foreach (var e in myExits.OrderBy(e => e.Dst, StringComparer.Ordinal))
                    sb.Append($"- → {ZoneLink(e.Dst)} (`{e.Rift}`)\n");
                sb.Append("\n");
            }
        }

        // Teleport map: the zone's own extent with its TP columns/landing spots plotted.
        if (maps.TryGetValue(name, out var svg))
        {
            var slug = name.ToLowerInvariant().Replace("_", "-");
            sb.Append("\n## Teleport map\n\n");
            sb.Append($"![Teleport columns in {title}](/zonemaps/{slug}.svg)\n\n");
            sb.Append("Where this zone's teleport columns stand (dots, labelled with the destination — dimmed where " +
                      "the column is currently switched off), the landing spots of teleports arriving from other " +
                      "zones (dashed circles), and the exit gates (diamonds) where one exists. Positions are the " +
                      "tile coordinates the server records.\n\n");
        }

        sb.Append($"[Zone index](/zones/zone-index/) · [World map](/zones/map/{MapAnchor(name, z)}) · [Protection levels](/zones/protection/)\n");
        return sb.ToString();
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
            or "zone_TM_pve" or "zone_ICS_pve" or "zone_ASI_pve") return "#starter-islands";
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
