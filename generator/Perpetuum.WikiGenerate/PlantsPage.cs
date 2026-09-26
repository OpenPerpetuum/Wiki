namespace Perpetuum.WikiGenerate;

public static class PlantsPage
{
    public static string Build(Db db, Dictionary<int, DefRow> defs, string plantrulesDir)
    {
        var zones = db.Query("SELECT id, name, fertility, plantruleset, zonetype, protected, enabled, plantaltitudescale, sparkcost, width, height FROM zones ORDER BY id")
            .Select(r => new ZoneRow(r.Int("id"), r.Str("name"), r.Int("fertility"), r.Int("plantruleset"), r.Int("zonetype"), r.Bit("protected"), r.Bit("enabled"), r.Dbl("plantaltitudescale"), r.Int("sparkcost"), r.Int("width"), r.Int("height")))
            .ToList();

        var refs = db.Query("SELECT idx, plantrule, rulesetid FROM plantrules ORDER BY rulesetid, idx")
            .Select(r => new PlantRuleRefRow(r.Int("idx"), r.Str("plantrule"), r.Int("rulesetid")))
            .ToList();

        // Load + parse each unique rule file (resolving "source" overrides), keyed by file name.
        var cache = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object> Load(string file)
        {
            if (cache.TryGetValue(file, out var cached)) return cached;
            var path = Path.Combine(plantrulesDir, file);
            if (!File.Exists(path))
            {
                cache[file] = new Dictionary<string, object> { ["_missing"] = "missing" };
                return cache[file];
            }
            var dict = Genxy.Parse(File.ReadAllText(path));
            if (dict.TryGetValue("source", out var src) && src is string srcName)
            {
                var baseRule = Load(srcName);
                dict.Remove("source");
                var merged = new Dictionary<string, object>(baseRule, StringComparer.OrdinalIgnoreCase);
                foreach (var kv in dict) merged[kv.Key] = kv.Value;
                cache[file] = merged;
            }
            else
            {
                cache[file] = dict;
            }
            return cache[file];
        }

        var sb = new StringBuilder();
        sb.Append(Md.Header("Plants", "Every plant species rule (growth, fertility, spreading, fruit) and the per-zone fertility settings.",
            "plantrules + $GameRoot/plantrules/*.txt (rule files), zones, entitydefaults (fruit names)"));
        sb.Append("\n\n# Plants\n\n");
        sb.Append("Plants grow over time through a sequence of stages; a plant produces (is harvestable) only once it reaches its fruiting stage. The zone maintains plant populations toward a **fertility target** per area; `spreading` biases new growth toward existing clusters of the same type. The full field reference — types, defaults, the growth state machine, and the rule-file format — is in [Plant fields](/formats/plant-fields/).\n\n");
        sb.Append("Field meanings: **growRate** = growth cycles a plant sits in each stage before advancing (higher = slower); **fertility** = how strongly the zone tries to keep this species present; **spreading** = preference for growing in groups; **killDistance** = minimum spacing between two plants of the same type (−1 = none);\n\n");

        sb.Append("## Plant species\n\n");
        var species = new List<(int Idx, string File, Dictionary<string, object> Rule)>();
        foreach (var f in refs.Select(r => r.File).Distinct())
        {
            var rule = Load(f);
            species.Add((0, f, rule));
        }

        var rows = species.Select(s =>
        {
            var r = s.Rule;
            int? fruitDef = r.Get<int>("fruitDefinition");
            string fruit = fruitDef is > 0 && defs.TryGetValue(fruitDef.Value, out var fd) ? $"{fd.Name} (×{Genxy.FormatValue(r.Get<int>("fruitAmount"))})" : (fruitDef is < 0 ? "none (not harvestable)" : fruitDef?.ToString() ?? "–");
            return new[]
            {
                r.GetOrDefault("name", s.File.Replace(".txt", "")),
                Genxy.FormatValue(r.GetOrDefault("growRate", 0)),
                Genxy.FormatValue(r.GetOrDefault("fertility", 0)),
                Genxy.FormatValue(r.GetOrDefault("spreading", 0)),
                Genxy.FormatValue(r.GetOrDefault("killDistance", 0)),
                Genxy.FormatValue(r.GetOrDefault("slope", 0)),
                $"{Genxy.FormatValue(r.GetOrDefault("allowedAltitudeLow", 0))}–{Genxy.FormatValue(r.GetOrDefault("allowedAltitudeHigh", 0))}",
                $"{Genxy.FormatValue(r.GetOrDefault("allowedWaterLevelLow", 0))}–{Genxy.FormatValue(r.GetOrDefault("allowedWaterLevelHigh", 0))}",
                Genxy.FormatValue(r.GetOrDefault("fruitingState", -1)),
                fruit,
                Genxy.FormatValue(r.GetOrDefault("maxAmount", -1)),
                Genxy.FormatValue(r.TryGetValue("health", out var h) ? h : null),
                r.TryGetValue("playerSeeded", out var ps) && Convert.ToInt32(ps) == 1 ? "yes" : "no",
                s.File
            };
        }).ToArray();
        Md.WriteTable(sb, new[]
        {
            "Plant", "Grow rate", "Fertility", "Spreading", "Kill distance", "Max slope", "Altitude band", "Water band", "Fruiting state", "Fruit (yield)", "Max amount", "Health (stages)", "Player seeded", "Rule file"
        }, rows);
        sb.Append('\n');

        sb.Append("## Zones\n\n");
        sb.Append("Per-zone fertility (how full the ground is kept with plants), the plant ruleset the zone uses, and altitude scaling.\n\n");
        var zoneRows = zones.Select(z => new[]
        {
            Md.ZoneName(z.Name),
            z.ZoneType.ToString(),
            z.Protected ? "yes" : "no",
            z.Fertility.ToString(),
            z.PlantRuleSet.ToString(),
            Md.Cell(z.PlantAltitudeScale),
            z.Enabled ? "yes" : "no"
        }).ToArray();
        Md.WriteTable(sb, new[] { "Zone", "Type", "Protected", "Fertility", "Plant ruleset", "Altitude scale", "Enabled" }, zoneRows);
        sb.Append('\n');

        sb.Append("## Ruleset → species\n\n");
        var rulesets = refs.GroupBy(r => r.RuleSet).OrderBy(g => g.Key);
        var rsRows = rulesets.Select(g => new[]
        {
            g.Key.ToString(),
            string.Join(", ", zones.Where(z => z.PlantRuleSet == g.Key).Select(z => Md.ZoneName(z.Name)).Distinct()) ?? "–",
            string.Join(", ", g.Select(r => r.File.Replace(".txt", "")).Distinct())
        }).ToArray();
        Md.WriteTable(sb, new[] { "Ruleset", "Zones", "Species" }, rsRows);

        return sb.ToString();
    }
}
