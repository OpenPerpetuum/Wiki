namespace Perpetuum.WikiGenerate;

/// <summary>
/// The deployables catalog: a card index (one card per deployable, grouped in
/// categories) plus one page per deployable under deployables/. The old single
/// 224-row table was replaced — cards carry the minimal info, the per-deployable
/// page carries the full stats.
/// </summary>
public static class DeployablesPage
{
    public static string Build(Db db, Dictionary<int, DefRow> defs, Dictionary<int, List<string>> statsByDef)
        => IndexBuild(Load(db), defs, statsByDef);

    /// <summary>One page per deployable under deployables/&lt;slug&gt;.md.</summary>
    public static List<(string File, string Content)> BuildPages(Db db, Dictionary<int, List<string>> statsByDef)
        => PagesBuild(Load(db), statsByDef);

    private static List<DefRow> Load(Db db)
        => db.Query("SELECT definition, definitionname, attributeflags, categoryflags, options, note, enabled, hidden, volume, mass, health, tiertype, tierlevel FROM entitydefaults ORDER BY definitionname")
            .Where(r => r.HasFlag("categoryflags", Flags.CfDeployableStructure) || (r.Lng("attributeflags") & Flags.AtDeployable) == Flags.AtDeployable)
            .Select(r => new DefRow(r.Int("definition"), r.Str("definitionname"), r.Lng("attributeflags"), r.Lng("categoryflags"),
                r.Str("options"), r.Str("note"), r.Bit("enabled"), r.Bit("hidden"), r.Dbl("volume"), r.Dbl("mass"), r.Dbl("health"), 0, r.Int("tiertype"), r.Int("tierlevel")))
            .Where(r => r.Enabled && !r.Hidden)
            .ToList();

    /// <summary>URL slug (same shape as the item slugs: no def_, dashes).</summary>
    public static string Slug(string name) => name["def_".Length..].ToLowerInvariant().Replace('_', '-');

    public static string Url(DefRow d) => "/content/deployables/" + Slug(d.Name) + "/";

    /// <summary>
    /// Category of a deployable, by name (first rule wins). The order is the
    /// page order: player-facing play categories first, the long PBS list in
    /// the middle, the one-offs last.
    /// </summary>
    public static (string Cat, string Blurb) Classify(string name)
    {
        var n = name["def_".Length..];
        if (n.StartsWith("pbs_")) return ("Power base stations", "The PBS structure types: what each builds, and in which size (small / medium / large).");
        if (n.StartsWith("mobile_arena")) return ("Arena teleports", "Teleport devices that move combatants and audience between the arena and the outer zones.");
        if (n.StartsWith("mobile_world_teleport") || n.StartsWith("mobile_teleport"))
            return ("Mobile teleports", "Deployable teleports a player places in the terrain (see [Teleporting](/features/movement/)).");
        if (n.StartsWith("mobile_field")) return ("Mobile fields", "Deployable effect fields: ECCM, maskers and the reactor stabilizer.");
        if (n.StartsWith("npc_egg")) return ("NPC eggs", "Capsules that hatch an NPC unit when placed (bought in the [NPC egg shop](/content/shop/npc-eggs/)).");
        if (n.StartsWith("plant_seed")) return ("Plant seeds", "Seeds that grow the matching plant species when planted (see [Plants](/content/plants/)).");
        if (n.Contains("landmine") || n.Contains("bomb")) return ("Mines & bombs", "Deployed explosives: the landmine sizes, wall bombs, the area bomb and plant bombs.");
        if (n.StartsWith("wall_healer")) return ("Wall repairers", "Deployed repairers that heal the wall structures.");
        if (n == "gate_capsule") return ("Gates", "The zone gate capsule.");
        if (n == "container_capsule") return ("Containers", "The deployable container capsule.");
        return ("Miscellaneous", "Everything else that can be placed in the terrain: beacons, probes, buoys, and the test deployables.");
    }

    private static string IndexBuild(List<DefRow> rows, Dictionary<int, DefRow> defs, Dictionary<int, List<string>> statsByDef)
    {
        var sb = new StringBuilder();
        sb.Append(Md.Header("Deployables", "Deployable structures and placeable items: one card per deployable, one page per deployable.",
            "entitydefaults (cf_deployable_structure / deployable attribute flag), aggregatevalues"));
        sb.Append("\n\n# Deployables\n\n");
        var listedCount = rows.Count(d => Classify(d.Name).Cat != "Power base stations");
        sb.Append($"Deployables are items that can be **placed in the terrain** to act on the zone — effect suppliers, repairers, turrets, teleports, and similar. Placing one is a corporation/privilege-gated action. The {listedCount} non-PBS deployables are listed below, one page each: **click a card to open it.** The PBS structures are documented on the [Power base stations](/features/pbs/) page. How deployables are identified, paired capsule↔object, and which stat fields drive them is in [Deployable fields](/formats/deployable-fields/).\n\n");

        // The Power base stations category is NOT listed here: the PBS page
        // (/features/pbs/) already documents those structures, and listing
        // 87 of them here only buries the rest. Their per-deployable pages
        // are still generated (BuildPages) and linked from the PBS page.
        var groups = rows
            .Where(d => Classify(d.Name).Cat != "Power base stations")
            .GroupBy(d => Classify(d.Name).Cat)
            .OrderBy(g => OrderOf(g.Key)).ToList();
        foreach (var g in groups)
        {
            var blurb = Classify(g.First().Name).Blurb;
            var items = g.ToList();
            sb.Append($"## {g.Key} ({items.Count})\n\n{blurb}\n\n");
            sb.Append("<div class=\"prod-cards\">\n");
            foreach (var d in items.OrderBy(d => d.Name, StringComparer.Ordinal))
            {
                var tier = Md.Tier(d.TierType, d.TierLevel);
                var meta = tier.Length > 0 ? tier : "no tier";
                var icon = IconFor(d.Name);
                sb.Append($"<div class=\"prod-card\"><div class=\"prod-card-head\"><svg class=\"prod-card-icon\" aria-hidden=\"true\"><use href=\"#{icon}\"/></svg><a class=\"prod-card-name\" href=\"{Url(d)}\">{ItemsPage.Esc(Md.DisplayName(d.Name))}</a></div><div class=\"prod-card-body\">{meta}</div></div>\n");
            }
            sb.Append("</div>\n\n");
        }
        return sb.ToString();
    }

    private static int OrderOf(string cat) => cat switch
    {
        "Power base stations" => 0,
        "Mobile teleports" => 1,
        "Mobile fields" => 2,
        "Arena teleports" => 3,
        "Mines & bombs" => 4,
        "Wall repairers" => 5,
        "Plant seeds" => 6,
        "NPC eggs" => 7,
        "Gates" => 8,
        "Containers" => 9,
        _ => 10,
    };

    private static string IconFor(string name)
    {
        var n = name["def_".Length..];
        if (n.StartsWith("pbs_")) return "mi-panel";
        if (n.StartsWith("mobile_") && (n.Contains("teleport") || n.Contains("arena"))) return "mi-map";
        if (n.StartsWith("mobile_field")) return "mi-radar";
        if (n.StartsWith("npc_egg")) return "mi-robots";
        if (n.StartsWith("plant_seed")) return "mi-tree";
        if (n.Contains("landmine") || n.Contains("bomb")) return "mi-star";
        if (n.StartsWith("wall_healer")) return "mi-tree";
        if (n == "gate_capsule") return "mi-grid";
        if (n == "container_capsule") return "mi-home";
        return "mi-grid";
    }

    private static List<(string File, string Content)> PagesBuild(List<DefRow> rows, Dictionary<int, List<string>> statsByDef)
    {
        var byName = rows.ToDictionary(d => d.Name);
        var pages = new List<(string File, string Content)>();
        foreach (var d in rows.OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            var sb = new StringBuilder();
            var name = Md.DisplayName(d.Name);
            var (cat, _) = Classify(d.Name);
            var tier = Md.Tier(d.TierType, d.TierLevel);
            sb.Append(Md.Header(name, $"{cat}: {name}",
                $"entitydefaults definition {d.Definition}, aggregatevalues via aggregatefields"));
            sb.Append($"\n\n# {name}\n\n");
            var rows2 = new List<string[]>
            {
                new[] { "Definition", $"`{d.Name}`" },
                new[] { "Category", cat },
            };
            if (tier.Length > 0) rows2.Add(new[] { "Tier", tier });
            rows2.Add(new[] { "Health", Md.Cell(d.Health) });
            rows2.Add(new[] { "Volume", Md.Cell(d.Volume) });
            rows2.Add(new[] { "Mass", Md.Cell(d.Mass) });
            // capsule <-> object pairing: strip the suffix and look the twin up
            var twin = Twin(d.Name, byName);
            if (twin is not null)
                rows2.Add(new[] { d.Name.Contains("_capsule") ? "Deployed object" : "Carried in capsule", $"[{Md.DisplayName(twin.Name)}]({Url(twin)})" });
            if (!string.IsNullOrEmpty(d.Note)) rows2.Add(new[] { "Note", d.Note });
            Md.WriteTable(sb, new[] { "", "" }, rows2.ToArray());
            sb.Append('\n');
            if (statsByDef.TryGetValue(d.Definition, out var stats) && stats.Count > 0)
            {
                sb.Append("## Stats\n\n");
                var srows = stats.Select(s =>
                {
                    var i = s.IndexOf('=');
                    return new[] { i >= 0 ? s[..i] : s, i >= 0 ? s[(i + 1)..] : "–" };
                }).ToArray();
                Md.WriteTable(sb, new[] { "Field", "Value" }, srows);
            }
            sb.Append($"\n[All deployables](/content/deployables/) · [Deployable fields](/formats/deployable-fields/)\n");
            pages.Add(("deployables/" + d.Name["def_".Length..] + ".md", sb.ToString()));
        }
        return pages;
    }

    /// <summary>The capsule/object twin of a deployable, if it exists in the set.
    /// Pairing is by name: the "capsule" token sits where the object token does
    /// (mobile_teleport_<b>capsule</b>_standard ↔ mobile_teleport_<b>column</b>_standard,
    /// pbs_x ↔ pbs_x_<b>capsule</b>, mobile_arena_<b>teleport</b>er_x_<b>object</b> ↔
    /// mobile_arena_<b>teleport</b>er_x_<b>capsule</b>) — try the token swaps in order.</summary>
    private static DefRow? Twin(string name, Dictionary<string, DefRow> byName)
    {
        var candidates = new List<string>();
        if (!name.Contains("_capsule") && !name.Contains("_object") && !name.Contains("_column"))
            candidates.Add(name + "_capsule");          // mobile_field_eccm -> ..._capsule
        if (name.EndsWith("_capsule_pr")) candidates.Add(name[..^"_capsule_pr".Length]);
        if (name.EndsWith("_capsule") || name.EndsWith("_object") || name.EndsWith("_column"))
        {
            var i = name.LastIndexOf('_');
            var stem = name[..i];                       // drop the trailing token
            candidates.Add(stem);                       // pbs_x_capsule -> pbs_x
            candidates.Add(stem + "_capsule");          // pbs_x -> pbs_x_capsule
        }
        if (name.Contains("_capsule"))
        {
            var i = name.IndexOf("_capsule");
            var rest = name[(i + "_capsule".Length)..];
            candidates.Add(name[..i] + "_column" + rest);   // mobile tp capsule -> column
            candidates.Add(name[..i] + "_object" + rest);   // arena capsule -> object
            // the arena objects carry a "teleporter" stem the capsules don't:
            var tp = name[..i].Replace("_teleport", "_teleporter");
            if (tp != name[..i]) candidates.Add(tp + "_object" + rest);
        }
        if (name.Contains("_column"))
        {
            var i = name.IndexOf("_column");
            candidates.Add(name[..i] + "_capsule" + name[(i + "_column".Length)..]);
        }
        if (name.Contains("_object"))
        {
            var i = name.IndexOf("_object");
            var rest = name[(i + "_object".Length)..];
            candidates.Add(name[..i] + "_capsule" + rest);
            var tp = name[..i].Replace("_teleporter", "_teleport");
            if (tp != name[..i]) candidates.Add(tp + "_capsule" + rest);
        }
        foreach (var cand in candidates)
            if (cand != name && byName.TryGetValue(cand, out var d)) return d;
        return null;
    }
}
