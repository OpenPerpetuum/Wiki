using System.Text;

namespace Perpetuum.WikiGenerate;

/// <summary>
/// Player-facing scale reference for item stats (/content/stat-reference/):
/// what each stat field measures, its unit, its range across the whole catalog,
/// and example items at the small/medium/large ends — plus the powergrid/CPU
/// context (what a robot's capacity is, so a usage value can be read as a %).
/// </summary>
public static class StatsReferencePage
{
    /// <summary>Field name -> (unit, player-facing description). Only fields whose
    /// meaning is certain are documented; the rest stay in the generated stats
    /// without a unit (see formats/ for the full developer field list).</summary>
    private static readonly Dictionary<string, (string Unit, string Meaning)> Fields = new(StringComparer.Ordinal)
    {
        ["mass"] = ("kg", "Mass. Affects transport volume and some industrial calculations."),
        ["volume"] = ("m³", "Cubic volume in containers."),
        ["armor_max"] = ("hp", "Maximum armor hit points."),
        ["core_max"] = ("RP", "Maximum energy (core) capacity of a robot chassis."),
        ["powergrid_max"] = ("RP", "Maximum reaction power (RP) capacity of a robot chassis — how much RP its modules may use in total."),
        ["cpu_max"] = ("CU", "Maximum CPU capacity of a robot chassis — how much CPU its modules may use in total."),
        ["powergrid_usage"] = ("RP", "Reaction power a module drains from the robot's powergrid while active."),
        ["core_usage"] = ("RP", "Energy a module drains from the core while active."),
        ["cpu_usage"] = ("CU", "CPU a module uses while active."),
        ["core_recharge_time"] = ("s", "Time for the core to fully recharge after depletion."),
        ["armor_repair_amount"] = ("hp", "Armor repaired per cycle by an armor repairer."),
        ["cycle_time"] = ("s", "Time between two working cycles of a module (weapons, repairers, harvesters)."),
        ["ammo_reload_time"] = ("s", "Time to reload after firing."),
        ["damage_kinetic"] = ("hp", "Damage per shot (kinetic — railguns)."),
        ["damage_explosive"] = ("hp", "Damage per shot (explosive — missiles)."),
        ["damage_thermal"] = ("hp", "Damage per shot (thermal — lasers)."),
        ["damage_chemical"] = ("hp", "Damage per shot (chemical)."),
        ["damage_toxic"] = ("hp", "Damage per shot (toxic)."),
        ["optimal_range"] = ("m", "Range at which the module does full work; efficiency falls off beyond it."),
        ["least_optimal"] = ("m", "Range where the module is at its worst."),
        ["falloff"] = ("%", "How fast the effect drops between optimal and least optimal (100 = linear to zero)."),
        ["slope"] = ("%", "Altitude penalty per meter of height difference (mountains matter)."),
        ["signature_radius"] = ("m", "Apparent target size. Smaller is harder to hit."),
        ["speed_max"] = ("m/s", "Maximum movement speed with engines."),
        ["massiveness"] = ("×", "Mass multiplier (negative = lighter than it looks)."),
        ["shield_absorbtion"] = ("hp", "Shield absorption per hit (shields take this much per incoming hit)."),
        ["shield_radius"] = ("m", "Radius of a shield bubble."),
        ["resist_kinetic"] = ("%", "Kinetic damage reduction."),
        ["resist_explosive"] = ("%", "Explosive damage reduction."),
        ["resist_thermal"] = ("%", "Thermal damage reduction."),
        ["resist_chemical"] = ("%", "Chemical damage reduction."),
        ["sensor_strength"] = ("—", "How well the robot's sensors detect through stealth."),
        ["stealth_strength"] = ("—", "How well the robot hides from sensors."),
        ["energy_transfer_amount"] = ("RP", "Energy moved per cycle by an energy transfer module."),
        ["energy_vampired_amount"] = ("RP", "Energy drained per cycle by an energy drainer."),
        ["energy_neutralized_amount"] = ("RP", "Energy neutralized per cycle by an energy neutralizer."),
        ["accuracy"] = ("%", "Base hit accuracy."),
        ["locked_targets_max"] = ("targets", "How many targets can be locked at once."),
        ["locking_range"] = ("m", "Maximum distance at which a target can be locked."),
        ["locking_time"] = ("s", "Time to lock a target at optimal range."),
        ["default_effect_range"] = ("m", "Range of a module's secondary effect."),
        ["explosion_radius"] = ("m", "Blast radius of an explosive."),
    };

    public static string Build(Db db)
    {
        var defs = db.Query("SELECT definition, definitionname, enabled, hidden FROM entitydefaults")
            .ToDictionary(r => r.Int("definition"), r => (r.Str("definitionname"), r.Bit("enabled"), r.Bit("hidden")));
        var fields = db.Query("SELECT id, name FROM aggregatefields").ToDictionary(r => r.Int("id"), r => r.Str("name"));

        // Catalog-wide values per documented field, prototypes and test objects excluded.
        var values = db.Query("""
            SELECT av.definition, af.name, av.value, d.definitionname, d.categoryflags
            FROM aggregatevalues av
            JOIN aggregatefields af ON af.id = av.field
            JOIN entitydefaults d ON d.definition = av.definition
            WHERE d.enabled = 1 AND d.hidden = 0
              AND d.definitionname NOT LIKE '%_pr' AND d.definitionname NOT LIKE '%_cprg'
              AND d.definitionname NOT LIKE 'def_test%' AND d.definitionname NOT LIKE '%punchbag%'
              AND af.name NOT LIKE '%modifier'
            """).ToList();

        // defs here is name->(name, enabled, hidden); the flags live on the
        // values rows because the catalog exclusion rules need category flags
        // (ores and deployable structures get their own pages, not item pages).
        string ItemLink(int def, long flags)
        {
            if (!defs.TryGetValue(def, out var d)) return null;
            var (name, enabled, hidden) = d;
            if (!enabled || hidden) return null;
            if (name.StartsWith("def_npc_")) return null; // NPC unit fits: no catalog page
            if (name.EndsWith("_bot") || name.EndsWith("_bot_pr")) return "/content/robots/";
            if ((flags & Flags.CfOre) == Flags.CfOre) return null;
            if ((flags & Flags.CfDeployableStructure) == Flags.CfDeployableStructure) return null;
            var slug = name["def_".Length..].ToLowerInvariant().Replace('_', '-');
            // "Capsule" items are their own catalog entry; when the stat actually
            // belongs to the payload (e.g. landmine damage), link the payload.
            if (slug.EndsWith("-capsule"))
            {
                var payload = "def_" + name["def_".Length..^8];
                if (defs.Values.Any(x => x.Item1 == payload))
                    return "/content/items/" + payload["def_".Length..].ToLowerInvariant().Replace('_', '-') + "/";
            }
            return "/content/items/" + slug + "/";
        }
        var flagsByDef = values
            .GroupBy(v => v.Int("definition"))
            .ToDictionary(g => g.Key, g => g.First().Lng("categoryflags"));
        string Example(int def)
        {
            var name = defs.TryGetValue(def, out var d) ? d.Item1 : "def_" + def;
            var url = ItemLink(def, flagsByDef.TryGetValue(def, out var f) ? f : 0);
            var label = Md.DisplayName(name);
            return url is null ? label : $"[{label}]({url})";
        }

        var sb = new StringBuilder();
        sb.Append(Md.Header("Stat reference", "What the item stats mean and how big each value is: units, catalog ranges, and example items at each end of the scale.",
            "aggregatevalues + aggregatefields (joined to entitydefaults)"));
        sb.Append("\n\n# Stat reference\n\n");
        sb.Append("Every item page lists raw stat values. This page puts them in scale: the unit of each stat, " +
                  "the smallest/largest values in the whole catalog, and real items to compare against. " +
                  "Ranges are computed over all enabled items (prototypes and test objects excluded).\n\n");

        // ---- powergrid / CPU context ----
        var chassis = db.Query("""
            SELECT d.definition, d.definitionname, av.value
            FROM entitydefaults d
            JOIN aggregatevalues av ON av.definition = d.definition
            JOIN aggregatefields af ON af.id = av.field
            WHERE af.name IN ('powergrid_max', 'core_max', 'cpu_max')
              AND d.definitionname LIKE '%_chassis' AND d.definitionname NOT LIKE '%_pr'
              AND d.enabled = 1 AND d.hidden = 0
            """).ToList();
        sb.Append("\n## Powergrid and CPU in context\n\n");
        sb.Append("A module's **powergrid_usage** is only meaningful against the robot that carries it: the " +
                  "chassis defines the total RP (**powergrid_max**), energy (**core_max**) and CPU (**cpu_max**) " +
                  "its modules may use. **Powergrid upgrades** modules add a fixed amount of RP. A usage value " +
                  "is a percentage of the available capacity — for example a module with " +
                  "powergrid_usage 214 uses 214% of an Ikarus chassis (100 RP, nothing fits beyond the frame) " +
                  "but only ~3% of an Onyx chassis (6.6k RP).\n\n");
        var capRows = chassis
            .GroupBy(r => r.Str("definitionname"))
            .Select(g => g.First())
            .OrderBy(r => r.Str("definitionname"))
            .ToList();
        // capacities: one row per chassis with its three values
        var capQuery = db.Query("""
            SELECT d.definitionname AS n,
                   MAX(CASE WHEN af.name = 'powergrid_max' THEN av.value ELSE NULL END) AS pg,
                   MAX(CASE WHEN af.name = 'core_max'      THEN av.value ELSE NULL END) AS core,
                   MAX(CASE WHEN af.name = 'cpu_max'       THEN av.value ELSE NULL END) AS cpu
            FROM entitydefaults d
            JOIN aggregatevalues av ON av.definition = d.definition
            JOIN aggregatefields af ON af.id = av.field
            WHERE af.name IN ('powergrid_max', 'core_max', 'cpu_max')
              AND d.definitionname LIKE '%_chassis' AND d.definitionname NOT LIKE '%_pr'
              AND d.definitionname NOT LIKE 'def_test%'
              AND d.definitionname NOT LIKE '%test%'
              AND d.definitionname NOT LIKE '%punchbag%'
              AND d.definitionname NOT LIKE '%invis%'
              AND d.definitionname NOT LIKE '%gm_test%'
              AND d.definitionname NOT LIKE 'def_npc%'
              AND d.definitionname NOT LIKE '%police%'
              AND d.enabled = 1
            GROUP BY d.definitionname ORDER BY pg DESC
            """).ToList();
        var capCells = capQuery.Select(r => new[]
        {
            Example(defs.FirstOrDefault(kv => kv.Value.Item1 == "def_" + r.Str("n").Replace("_bot", "_bot")).Key) is { } _
                ? Example2(r.Str("n")) : r.Str("n"),
            r.TryGetValue("pg", out var pg) && pg is not null ? Md.Num(Convert.ToInt64(pg!)) : "–",
            r.TryGetValue("core", out var co) && co is not null ? Md.Num(Convert.ToInt64(co!)) : "–",
            r.TryGetValue("cpu", out var cp) && cp is not null ? Md.Num(Convert.ToInt64(cp!)) : "–",
        }).ToArray();
        string Example2(string defname)
        {
            if (!defs.Values.Any(d => d.Item1 == defname)) return Md.DisplayName(defname);
            var def = defs.First(kv => kv.Value.Item1 == defname).Key;
            // Robot frames are hidden from the item catalog; link to the robot page.
            return defname.EndsWith("_bot_chassis") || !defs[def].Item2
                ? $"[{Md.DisplayName(defname)}](/content/robots/)"
                : Example(def);
        }
        Md.WriteTable(sb, new[] { "Chassis (frame)", "Powergrid (RP)", "Core (RP)", "CPU" }, capCells);
        sb.Append('\n');

        // ---- scale table per documented field ----
        sb.Append("\n## Stat scale\n\n");
        sb.Append("For each documented stat: how many items have it, its unit, and the smallest / median / " +
                  "largest value in the catalog with an item you can open at each end.\n\n");
        var header = new[] { "Stat", "Unit", "Items", "Smallest", "Median", "Largest", "Small example", "Large example" };
        var cells = Fields.Keys
            .OrderBy(k => k)
            .Select(k =>
            {
                var rows = values.Where(v => v.Str("name") == k)
                    .Select(v => (Def: v.Int("definition"), Val: v.Dbl("value")))
                    .OrderBy(v => v.Val).ToList();
                if (rows.Count == 0) return null;
                var (unit, _) = Fields[k];
                var median = rows[rows.Count / 2].Val;
                return new[]
                {
                    $"`{k}`",
                    unit,
                    rows.Count.ToString(),
                    Md.Num(rows.First().Val),
                    Md.Num(median),
                    Md.Num(rows.Last().Val),
                    Example(rows.First().Def),
                    Example(rows.Last().Def),
                };
            })
            .Where(c => c is not null)
            .Cast<string[]>()
            .ToArray();
        Md.WriteTable(sb, header, cells);
        sb.Append('\n');
        sb.Append("\nStats not listed here are either **modifier** stats (they change another stat instead of " +
                  "setting one) or internal values without a player-facing unit — the developer field " +
                  "reference in [Formats](/formats/) lists every field.\n");
        return sb.ToString();
    }
}
