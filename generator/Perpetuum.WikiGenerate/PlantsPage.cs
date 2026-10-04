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

        // The fence must start at column 0: an indented fence is parsed as an
        // indented code block and never becomes a mermaid diagram.
        sb.Append("\n```mermaid\nflowchart LR\n" +
                  "    S[\"Sprout (stage 1)\"] --> G[\"Grow: growRate cycles per stage\"]\n" +
                  "    G --> S\n" +
                  "    G --> F[\"Fruiting stage reached\"]\n" +
                  "    F --> H[\"Harvestable: fruit x fruitAmount\"]\n" +
                  "    F --> K[\"Killed / damaged\"]\n" +
                  "    K --> R[\"Zone respawns toward the\\nfertility target (weighted by species fertility)\"]\n" +
                  "```\n");
        var species = new List<(int Idx, string File, Dictionary<string, object> Rule)>();
        foreach (var f in refs.Select(r => r.File).Distinct())
        {
            var rule = Load(f);
            species.Add((0, f, rule));
        }

        // Split harvestable (fruiting) species from the scenery plants: the
        // fruit column is otherwise always "none", and the raw fruitingState
        // value is already visible in the fruit column.
        var parsed = species.Select(s =>
        {
            var r = s.Rule;
            int? fruitDef = r.Get<int>("fruitDefinition");
            bool harvestable;
            string fruit;
            if (fruitDef is > 0 && defs.TryGetValue(fruitDef.Value, out var fd))
            {
                harvestable = true;
                fruit = $"{Md.DisplayName(fd.Name)} (×{Genxy.FormatValue(r.Get<int>("fruitAmount"))})";
            }
            else
            {
                harvestable = false;
                fruit = "–";
            }
            return (Name: r.GetOrDefault("name", s.File.Replace(".txt", "")),
                    GrowRate: Genxy.FormatValue(r.GetOrDefault("growRate", 0)),
                    Fertility: Genxy.FormatValue(r.GetOrDefault("fertility", 0)),
                    Spreading: Genxy.FormatValue(r.GetOrDefault("spreading", 0)),
                    KillDistance: Genxy.FormatValue(r.GetOrDefault("killDistance", 0)),
                    Slope: Genxy.FormatValue(r.GetOrDefault("slope", 0)),
                    Altitude: $"{Genxy.FormatValue(r.GetOrDefault("allowedAltitudeLow", 0))}–{Genxy.FormatValue(r.GetOrDefault("allowedAltitudeHigh", 0))}",
                    Water: $"{Genxy.FormatValue(r.GetOrDefault("allowedWaterLevelLow", 0))}–{Genxy.FormatValue(r.GetOrDefault("allowedWaterLevelHigh", 0))}",
                    Fruit: fruit,
                    MaxAmount: Genxy.FormatValue(r.GetOrDefault("maxAmount", -1)),
                    Health: Genxy.FormatValue(r.TryGetValue("health", out var h) ? h : null),
                    Seeded: r.TryGetValue("playerSeeded", out var ps) && Convert.ToInt32(ps) == 1 ? "yes" : "no",
                    Harvestable: harvestable);
        }).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

        sb.Append("## Harvestable plants\n\n");
        sb.Append("Species with a fruiting stage — the fruit is the harvestable yield (× = fruitAmount per fruiting stage).\n\n");
        var harvRows = parsed.Where(x => x.Harvestable).Select(x => new[]
        {
            x.Name, x.GrowRate, x.Fertility, x.Spreading, x.KillDistance, x.Slope, x.Altitude, x.Water, x.Fruit, x.MaxAmount, x.Health, x.Seeded
        }).ToArray();
        Md.WriteTable(sb, new[]
        {
            "Plant", "Grow rate", "Fertility", "Spreading", "Kill distance", "Max slope", "Altitude band", "Water band", "Fruit (yield)", "Max amount", "Health (stages)", "Player seeded"
        }, harvRows);
        sb.Append('\n');

        sb.Append("## Scenery plants (not harvestable)\n\n");
        sb.Append("Species with no fruiting stage — zone scenery only (cover, landmarks, blocking).\n\n");
        var sceneRows = parsed.Where(x => !x.Harvestable).Select(x => new[]
        {
            x.Name, x.GrowRate, x.Fertility, x.Spreading, x.KillDistance, x.Slope, x.Altitude, x.Water, x.MaxAmount, x.Health, x.Seeded
        }).ToArray();
        Md.WriteTable(sb, new[]
        {
            "Plant", "Grow rate", "Fertility", "Spreading", "Kill distance", "Max slope", "Altitude band", "Water band", "Max amount", "Health (stages)", "Player seeded"
        }, sceneRows);
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
