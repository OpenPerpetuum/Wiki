namespace Perpetuum.WikiGenerate;

public static class RobotsPage
{
    // Robot class bits encoded in the high part of categoryflags (see CategoryFlags:
    // cf_runner_head = 0x10150, cf_crawler_head = 0x20150, cf_mech_head = 0x30150,
    // cf_heavymech_head = 0x40150, cf_walker_head = 0x50150).
    private const long BitRunner = 0x10000, BitCrawler = 0x20000, BitMech = 0x30000,
                       BitHeavyMech = 0x40000, BitWalker = 0x50000;

    private static readonly (long Bit, string Name, string Intro, string Pros, string Cons)[] Archetypes =
    {
        (BitRunner, "Runners", "The fast, light class — the cheapest robots to build and pilot.",
         "Fast and agile; cheap parts; fine for travel, couriers and scouting.",
         "Few slots and the weakest base stats of the combat classes."),
        (BitCrawler, "Crawlers", "The mid-size workhorse class.",
         "Balanced speed, slots and durability; the standard choice for gathering and hauling.",
         "No single extreme — outgunned by mechs, outrun by runners."),
        (BitMech, "Mechs", "The main combat class.",
         "More slots and durability than crawlers — the usual platform for combat fitting.",
         "Slower and more expensive to build and maintain."),
        (BitHeavyMech, "Heavy mechs", "The top-tier combat class.",
         "The strongest base stats and the most slots; built for sustained fights.",
         "Slow and the most expensive parts in the game."),
        (BitWalker, "Walkers", "A rare heavy platform class.",
         "Heavy-mech-class stats in a distinct platform.",
         "Very few models; limited part availability."),
    };

    public static string Build(Db db, Dictionary<int, DefRow> defs, Dictionary<int, List<string>> statsByDef)
    {
        var robots = db.Query("SELECT definition, definitionname, categoryflags, enabled, hidden, volume, mass, health, tiertype, tierlevel FROM entitydefaults WHERE definitionname LIKE '%_bot' AND enabled = 1 AND hidden = 0 AND definitionname NOT LIKE '%tutorial%' AND definitionname NOT LIKE '%spectator%' ORDER BY definitionname")
            .Select(r => new DefRow(r.Int("definition"), r.Str("definitionname"), 0, r.Lng("categoryflags"),
                "", "", r.Bit("enabled"), r.Bit("hidden"), r.Dbl("volume"), r.Dbl("mass"), r.Dbl("health"), 0, r.Int("tiertype"), r.Int("tierlevel")))
            .ToList();

        var purchasable = db.Query("SELECT definition, purchasable FROM entitydefaults WHERE definitionname LIKE '%_bot'")
            .ToDictionary(r => r.Int("definition"), r => r.Bit("purchasable"));

        var turrets = robots.Where(d => d.Name.Contains("turret")).ToList();
        var hybrids = robots.Where(d => d.Name.Contains("hybrid")).ToList();
        // "syndicate forces" entries are named fits (volume 0, no class bits): complete
        // loadouts unlocked through the tech tree rather than standalone chassis.
        var namedFits = robots.Where(d => d.Name.Contains("syndicate")).ToList();
        bool NotPurchasable(DefRow d) => !purchasable.TryGetValue(d.Definition, out var p) || !p;
        var npc = robots.Where(d => (!d.Name.Contains("turret") && !d.Name.Contains("hybrid") && NotPurchasable(d))
                                     || (namedFits.Contains(d) && NotPurchasable(d))).ToList();
        var player = robots.Where(d => !turrets.Contains(d) && !hybrids.Contains(d) && !namedFits.Contains(d) && !npc.Contains(d)).ToList();
        var playerNamedFits = namedFits.Where(d => !npc.Contains(d)).ToList();

        var classGroups = Archetypes
            .Select(a => (a.Name, a.Intro, a.Pros, a.Cons, Rows: player.Where(d => ClassOf(d) == a.Bit).ToList()))
            .ToList();
        var special = (Name: "Starter & special",
            Intro: "The starter bot, the flagship and limited/event models.",
            Pros: "Each is a one-off — check the model's parts for its actual stats.",
            Cons: "Limited availability (starter, anniversary or event models).",
            Rows: player.Where(d => ClassOf(d) == 0).ToList());

        var sb = new StringBuilder();
        sb.Append(Md.Header("Robots", "How a robot is built, the robot classes, and every robot: player, hybrid, NPC and turret.",
            "entitydefaults (complete robot definitions, robot class part flags, purchasable flag)"));
        sb.Append("\n\n# Robots\n\n");
        sb.Append("Fitting, slots and in-game play are covered in the [Robots & fitting](/features/robots/) feature page; this page is the data reference.\n\n");
sb.Append(@"
        ```mermaid
        flowchart TD
            R[""Robots""] --> P[""Player robots""]
            P -->|fast, light, cheap| RU[""Runners""]
            P -->|compact workers| CR[""Crawlers""]
            P -->|combat workhorses| ME[""Mechs""]
            P -->|top-tier combat| HM[""Heavy mechs""]
            P -->|rare heavy platform| WA[""Walkers""]
            P -->|one-off models| SP[""Starter & special""]
            R --> HY[""Hybrid builds""]
            R --> NP[""NPC units""]
            R --> TU[""Defense turrets""]
        ```
        
        ");

        sb.Append("## How a robot is made\n\n");
        sb.Append("A robot you control is assembled from **three body parts** — a **head**, a **chassis** and a set of **legs** — plus a **cargo container**. Each part contributes to the robot's stats (core, CPU, power grid, armor, speed, …) and provides **module slots**; modules are fitted into the slots of the part that carries them. A module only fits a slot whose category flags cover the module's own flags (see [slot categories](/features/robots/#slot-categories)).\n\n");
        sb.Append("Individual parts (heads, chassis, legs, containers) are listed under [Items → Robot components](/content/items/). Most models come in two **generations** — the base model and an improved **MK2** — and a few are limited or event models.\n\n");

        sb.Append("## Robot classes\n\n");
        sb.Append("A robot's class comes from its parts. Typical roles below are guidance — check the per-model numbers for anything specific.\n\n");
        sb.Append("| Class | Role | Pros | Cons |\n|---|---|---|---|\n");
        foreach (var (bit, name, intro, pros, cons) in Archetypes)
        {
            sb.Append($"| **{name}** | {intro} | {pros} | {cons} |\n");
        }
        sb.Append('\n');

        sb.Append("## Player robots\n\n");
        sb.Append($"Robots you can acquire and control yourself ({player.Count} models), grouped by class; each model gets its own table with its generations.\n\n");
        sb.Append("- **Size** — the space the packed robot takes up inside a container when you store or transport it (bigger robots need bigger containers).\n");
        sb.Append("- **Availability** — robots are not restricted to specific zones; you unlock models through [research](/features/research/) or buy them (see the [shop](/content/shop/)). Zone tiers (gamma zones) only limit the [PBS](/features/pbs/) structures you can deploy there.\n\n");

        foreach (var (name, intro, pros, cons, rows) in classGroups)
        {
            if (rows.Count == 0) continue;
            sb.Append($"### {name} ({rows.Count})\n\n");
            sb.Append(intro + "\n\n");
            sb.Append($"*Strengths:* {pros}\n\n*Weaknesses:* {cons}\n\n");
            WriteFamilies(sb, rows);
        }
        if (special.Rows.Count > 0)
        {
            sb.Append($"### {special.Name} ({special.Rows.Count})\n\n");
            sb.Append(special.Intro + "\n\n");
            sb.Append($"*Strengths:* {special.Pros}\n\n*Weaknesses:* {special.Cons}\n\n");
            WriteFamilies(sb, special.Rows);
        }

        if (playerNamedFits.Count > 0)
        {
            sb.Append($"### Named fits ({playerNamedFits.Count})\n\n");
            sb.Append("Complete cult-designed loadouts unlocked through research — a finished fit rather than a standalone chassis (size comes from the parts it is built from).\n\n");
            Md.WriteTable(sb, new[] { "Robot", "Class" },
                playerNamedFits.Select(d => new[] { Md.DisplayName(d.Name), ClassName(ClassOf(d)) }).ToArray());
            sb.Append("\n*Strengths:* a ready-made combat build with its role in the name (main combat / main support).\n");
            sb.Append("*Weaknesses:* fixed by the design — you take the fit as it comes.\n\n");
        }

        if (hybrids.Count > 0)
        {
            sb.Append($"## Hybrid builds ({hybrids.Count})\n\n");
            sb.Append("Mixed-class builds assembled from parts of different robot classes rather than one pure class. They are built from parts, not sold as a finished robot; the finished size comes from the parts chosen.\n\n");
            Md.WriteTable(sb, new[] { "Robot" },
                hybrids.Select(d => new[] { Md.DisplayName(d.Name) }).ToArray());
            sb.Append("\n*Strengths:* mix the advantages of several classes in one build.\n");
            sb.Append("*Weaknesses:* you must assemble the parts yourself — no single finished product to buy.\n\n");
        }

        if (npc.Count > 0)
        {
            sb.Append($"## NPC units ({npc.Count})\n\n");
            sb.Append("Combat units fielded by the NPC factions (not acquirable by players).\n\n");
            Md.WriteTable(sb, new[] { "Unit" },
                npc.Select(d => new[] { Md.DisplayName(d.Name) }).ToArray());
            sb.Append('\n');
        }

        if (turrets.Count > 0)
        {
            sb.Append($"## Defense turrets ({turrets.Count})\n\n");
            sb.Append("Not player-controllable — deployed as zone/PBS defense structures (see [Power base stations](/features/pbs/)).\n\n");
            Md.WriteTable(sb, new[] { "Turret" },
                turrets.Select(d => new[] { Md.DisplayName(d.Name) }).ToArray());
            sb.Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>One table per model family (base + MK2 + reward variants). The
    /// class strengths/weaknesses are written once per class section, not per model.</summary>
    private static void WriteFamilies(StringBuilder sb, List<DefRow> rows)
    {
        foreach (var family in rows.GroupBy(FamilyKey).OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var frows = family
                .OrderBy(d => d.Name.Contains("_mk2") ? 1 : d.Name.Contains("_reward") ? 2 : 0)
                .ThenBy(d => d.Name, StringComparer.Ordinal)
                .ToList();
            var title = Md.DisplayName(family.Key);
            sb.Append($"**{title}**\n\n");
            // Model icon. Real model icons are not bundled (see the reuse policy);
            // every family shows the shared placeholder until licensed art exists.
            // RobotIcons can map a family to a file in static/img/robots/ again.
            // The placeholder is a CSS alpha-mask (theme-tinted); real icons
            // (when licensed art exists in RobotIcons) are plain <img>.
            if (RobotIcons.TryGetValue(title, out var ic))
                sb.Append($"<img class=\"robot-icon\" src=\"/img/robots/{ic}\" alt=\"{title}\">\n\n");
            else
                sb.Append($"<span class=\"robot-icon icon-mask\" role=\"img\" aria-label=\"{title} icon\"></span>\n\n");
            Md.WriteTable(sb, new[] { "Robot", "Size" },
                frows.Select(d => new[] { Md.DisplayName(d.Name), Md.Cell(d.Volume) }).ToArray());
            sb.Append('\n');
        }
    }

    /// <summary>Model family key: the definition name without generation/reward suffixes.</summary>
    private static string FamilyKey(DefRow d) => d.Name.Replace("_mk2", "").Replace("_reward1", "");

    /// <summary>Display name -> static icon file (static/img/robots/). Empty for
    /// now: the community-wiki icons were removed from the repo and the client
    /// archive is not an allowed source. Families with no entry fall back to
    /// placeholder.svg; add licensed icons here when available.</summary>
    private static readonly Dictionary<string, string> RobotIcons = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    /// <summary>Class by priority (higher classes set the lower class bits too).</summary>
    private static long ClassOf(DefRow d)
    {
        if ((d.CatFlags & BitWalker) == BitWalker) return BitWalker;
        if ((d.CatFlags & BitHeavyMech) == BitHeavyMech) return BitHeavyMech;
        if ((d.CatFlags & BitMech) == BitMech) return BitMech;
        if ((d.CatFlags & BitCrawler) == BitCrawler) return BitCrawler;
        if ((d.CatFlags & BitRunner) == BitRunner) return BitRunner;
        return 0;
    }

    private static string ClassName(long c) => c switch
    {
        BitRunner => "Runner",
        BitCrawler => "Crawler",
        BitMech => "Mech",
        BitHeavyMech => "Heavy mech",
        BitWalker => "Walker",
        _ => "—",
    };
}
