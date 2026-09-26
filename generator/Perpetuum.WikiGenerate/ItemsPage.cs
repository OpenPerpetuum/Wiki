namespace Perpetuum.WikiGenerate;

/// <summary>
/// The item catalog. Each item gets its own page under
/// wiki/content/content/items/&lt;slug&gt;.md; items/_index.md lists the
/// categories and sub-categories with a link per item. Every item is classified
/// exactly once (categories + sub-categories), so a name never repeats across
/// several tables.
/// </summary>
public static class ItemsPage
{
    /// <summary>Top-level categories, in page order, with their sub-categories (page
    /// order) and a blurb for the category heading.</summary>
    private static readonly (string Cat, string Blurb, string[] Subs)[] Categories =
    {
        ("Modules", "Equipable robot modules (weapons, sensors, shield/armor systems, power, utilities).",
            new[] { "Weapons", "Turrets", "Sensors & scanning", "Shield", "Armor", "Power", "Repair", "Remote control", "Harvesting", "Enhancements", "Other modules" }),
        ("Ammo", "Ammunition for active weapon modules.",
            new[] { "Mining", "Missiles", "Projectiles", "Beam & laser", "Cannon", "Harvesting", "Other ammo" }),
        ("Robot parts", "Robot parts (heads, chassis, legs) used to build robots.",
            new[] { "Heads", "Chassis", "Legs" }),
        ("Materials", "Raw and processed materials used in production. (Ores have their own page.)",
            new[] { "General" }),
        ("Production", "Production templates and production items.",
            new[] { "General" }),
        ("Mission items", "Items and materials related to field missions.",
            new[] { "General", "Landmarks" }),
        ("Artifacts", "Artifact items.",
            new[] { "General" }),
        ("Decorations", "Zone decorations and placeable scenery.",
            new[] { "General" }),
        ("Special & other", "Everything else: spark unlocks, containers, robot fit capsules, misc. (CT capsules and calibrated variants of modules/ammo are listed with their base item.)",
            new[] { "Spark unlocks", "Containers", "Fit capsules & programs", "Miscellaneous" }),
    };

    public static List<(string File, string Content)> Build(Db db, Dictionary<int, DefRow> defs, Dictionary<int, List<string>> statsByDef,
        List<ShopSale> shop)
    {
        var defsByName = defs.Values.ToDictionary(d => d.Name, d => d);

        // Shop vendors per item (for the "Where to buy" section on item pages).
        var whereToBuy = shop.Count > 0
            ? shop.Select(s => s.Def).Distinct()
                .ToDictionary(d => d, d => ShopPage.VendorsFor(shop, d))
            : new Dictionary<int, List<(ShopPage.Location Loc, int Qty, long Tm, long Ics, long Asi, long Credit, long Uni, double Standing)>>();

        // Production data for the CT-capsule pages (payload research level + components).
        var research = db.Query("SELECT definition, MAX(researchlevel) AS lvl FROM itemresearchlevels GROUP BY definition")
            .ToDictionary(r => r.Int("definition"), r => r.Int("lvl"));
        var components = db.Query("SELECT definition, componentdefinition, componentamount FROM components")
            .GroupBy(r => r.Int("definition"))
            .ToDictionary(g => g.Key, g => g.Select(r => (Comp: r.Int("componentdefinition"), Amt: r.Int("componentamount"))).ToList());
        var all = db.Query("SELECT definition, definitionname, attributeflags, categoryflags, enabled, hidden, volume, mass, health, quantity, tiertype, tierlevel, note FROM entitydefaults ORDER BY definitionname")
            .Select(r => new DefRow(r.Int("definition"), r.Str("definitionname"), r.Lng("attributeflags"), r.Lng("categoryflags"),
                "", r.Str("note"), r.Bit("enabled"), r.Bit("hidden"), r.Dbl("volume"), r.Dbl("mass"), r.Dbl("health"), r.Int("quantity"), r.Int("tiertype"), r.Int("tierlevel")))
            .Where(d => d.Enabled && !d.Hidden)
            .ToList();

        // The item pool: real items only. Ores, robots, deployables and the internal
        // NPC unit fits (def_npc_*, robot flags but no _bot name) have their own pages
        // or no page at all.
        var items = all
            .Where(d => IsItem(d))
            .ToList();

        // Display names: the client string where it is unique across the catalog; a
        // shared/generic client string (e.g. "Interactive landmark" for every kiosk)
        // falls back to the name derived from the definition name.
        var dupes = new HashSet<string>(
            items.Select(d => Md.DisplayName(d.Name)).GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key));
        string ShowName(DefRow d) => dupes.Contains(Md.DisplayName(d.Name)) ? Md.DisplayName(d.Name, true) : Md.DisplayName(d.Name);

        var pages = new List<(string File, string Content)>();
        var byCat = new Dictionary<string, List<(DefRow D, (string Cat, string Sub) CS)>>();

        foreach (var d in items.OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            var cs = Classify(d, defsByName);
            var stats = statsByDef.GetValueOrDefault(d.Definition) ?? new List<string>();
            var production = d.Name.EndsWith("_CT_capsule")
                ? PayloadProduction(d.Name, defs, defsByName, research, components)
                : null;
            // The transport capsule of this item (for the reverse diagram), when it
            // exists and is a visible catalog item.
            var ct = defsByName.TryGetValue(d.Name + "_CT_capsule", out var ctRow) && ctRow.Enabled && !ctRow.Hidden
                ? ctRow
                : null;
            pages.Add((ItemPath(d.Name), ItemPage(d, ShowName(d), cs, stats, production, ct,
                whereToBuy.TryGetValue(d.Definition, out var vendors) ? vendors : null)));
            if (!byCat.TryGetValue(cs.Cat, out var list)) byCat[cs.Cat] = list = new List<(DefRow, (string, string))>();
            list.Add((d, cs));
        }

        // The category index.
        var sb = new StringBuilder();
        sb.Append(Md.Header("Items", "Every item with its own stat page: modules, ammo, armor, robot parts, materials, and more — by category and sub-category.",
            "entitydefaults (enabled, non-hidden items), aggregatevalues via aggregatefields"));
        sb.Append("\n\n# Items\n\n");
        sb.Append("Every item the game offers players, grouped into categories and sub-categories — " +
                  "**each item has its own page** with its full stats. Tier: 1 = normal, 2 = prototype, " +
                  "3 = special. What a stat field actually does is documented in [Formats](/formats/).\n\n");

        foreach (var (cat, blurb, subs) in Categories)
        {
            if (!byCat.TryGetValue(cat, out var rows)) continue;
            sb.Append($"## {cat} ({rows.Count})\n\n{blurb}\n\n");
            foreach (var sub in subs)
            {
                // Tier first (0-5, untyped last), then name: the listing order is
            // the progression order, not alphabetical.
                var subRows = rows.Where(r => r.CS.Sub == sub)
                    .OrderBy(r => TierSortKey(r.D))
                    .ThenBy(r => r.D.Name, StringComparer.Ordinal)
                    .ToList();
                if (subRows.Count == 0) continue;
                sb.Append($"### {sub} ({subRows.Count})\n\n");
                Md.WriteTable(sb, new[] { "Item", "Tier" },
                    subRows.Select(r => new[]
                    {
                        $"[{ShowName(r.D)}](/content/items/{Slug(r.D.Name)}/)",
                        Md.Tier(r.D.TierType, r.D.TierLevel)
                    }).ToArray());
                sb.Append('\n');
            }
        }

        pages.Insert(0, ("items/_index.md", sb.ToString()));
        return pages;
    }

    /// <summary>Sort key for catalog listings: tier 0-5 ascending, untyped items last.</summary>
    private static int TierSortKey(DefRow d) => d.TierType == 0 || d.TierLevel is null ? int.MaxValue : d.TierLevel.Value;

    private static bool IsItem(DefRow d)
    {
        if ((d.CatFlags & Flags.CfOre) == Flags.CfOre) return false;
        if ((d.CatFlags & Flags.CfDeployableStructure) == Flags.CfDeployableStructure) return false;
        if (d.Name.EndsWith("_bot")) return false;
        if (d.Name.StartsWith("def_npc_")) return false; // internal NPC unit fits
        if (d.Name.EndsWith("_bot_pr")) return false; // robot prototypes belong to the robot catalog
        return true;
    }

    /// <summary>Category + sub-category, decided once per item (first rule wins).</summary>
    private static (string Cat, string Sub) Classify(DefRow d, Dictionary<string, DefRow> defs)
    {
        // Calibrated (cprg) and CT-capsule variants are listed with their base item:
        // classify by the payload name and the payload's own category flags (the
        // variant definitions carry material-ish flags, not the base item's).
        var n = d.Name;
        if (n.EndsWith("_cprg")) n = n[..^5];
        else if (n.EndsWith("_CT_capsule")) n = n[..^11];
        var flags = d.CatFlags;
        if (n != d.Name && defs.TryGetValue(n, out var payload)) flags |= payload.CatFlags;
        bool Has(string w) => n.Contains(w);

        // Robot parts (by name; the components flag is set by many non-part items too).
        if (n.EndsWith("_head")) return ("Robot parts", "Heads");
        // Robot/fit CT capsules and dynamic calibration programs whose payload is not
        // a catalog item (robots and named fits) — grouped together.
        if (n != d.Name && !HasItemName(n, flags)) return ("Special & other", "Fit capsules & programs");
        if (n.EndsWith("_chassis")) return ("Robot parts", "Chassis");
        if (n.EndsWith("_leg") || n.EndsWith("_legs")) return ("Robot parts", "Legs");

        if (n.StartsWith("def_ammo_"))
        {
            var sub = n.StartsWith("def_ammo_mining") ? "Mining"
                : n.Contains("missile") ? "Missiles"
                : n.Contains("projectile") ? "Projectiles"
                : n.Contains("beam") || n.Contains("laser") ? "Beam & laser"
                : n.Contains("cannon") || n.Contains("railgun") ? "Cannon"
                : n.Contains("slug") ? "Projectiles"
                : n.Contains("harvesting") ? "Harvesting"
                : "Other ammo";
            return ("Ammo", sub);
        }

        if (n.StartsWith("def_production_")) return ("Production", "General");
        if (n.StartsWith("def_mission") || n.StartsWith("def_missionitem"))
            return ("Mission items", n.Contains("kiosk") || n.Contains("landmark") ? "Landmarks" : "General");
        if (n.StartsWith("def_artifact_")) return ("Artifacts", "General");
        if (n.StartsWith("def_decor_")) return ("Decorations", "General");

        // Special items by name, before the material flag (several carry it).
        if (n.Contains("spark_unlock")) return ("Special & other", "Spark unlocks");
        if (n.EndsWith("_container")) return ("Special & other", "Containers");
        if (n.Contains("package")) return ("Special & other", "Miscellaneous");

        // Modules: the equipment flag covers every equipable module (payload flags for
        // calibrated/capsule variants).
        if ((flags & Flags.CfRobotEquipment) == Flags.CfRobotEquipment)
        {
            var sub = Has("turret") ? "Turrets"
                : Has("laser") || Has("autocannon") || Has("railgun") || Has("_launcher") || Has("cannon") || Has("weapon") ? "Weapons"
                : Has("sensor") || Has("scanner") || Has("locator") ? "Sensors & scanning"
                : Has("shield") || Has("energy_neutralizer") || Has("vampire") ? "Shield"
                : Has("repairer") || Has("repair") ? "Repair"
                : Has("armor") ? "Armor"
                : Has("core_recharger") || Has("core_battery") || Has("battery") || Has("powergrid") || Has("capacitor") ? "Power"
                : Has("remote_controller") || Has("controller") || Has("remote_command") ? "Remote control"
                : Has("harvester") || Has("extractor") || Has("excavator") ? "Harvesting"
                : "Enhancements";
            return ("Modules", sub);
        }

        if ((flags & Flags.CfMaterial) == Flags.CfMaterial) return ("Materials", "General");

        return ("Special & other", "Miscellaneous");
    }

    /// <summary>Whether the (possibly payload) name matches one of the item rules above.</summary>
    private static bool HasItemName(string n, long flags)
    {
        if (n.StartsWith("def_ammo_")) return true;
        if (n.StartsWith("def_production_")) return true;
        if (n.StartsWith("def_mission") || n.StartsWith("def_missionitem")) return true;
        if (n.StartsWith("def_artifact_")) return true;
        if (n.StartsWith("def_decor_")) return true;
        if (n.Contains("spark_unlock") || n.EndsWith("_container") || n.Contains("package")) return true;
        if ((flags & Flags.CfRobotEquipment) == Flags.CfRobotEquipment) return true;
        if ((flags & Flags.CfMaterial) == Flags.CfMaterial) return true;
        return false;
    }

    /// <summary>URL slug for an item: the definition name without the def_ prefix,
    /// lowercased (Zola lowercases the URL slug, e.g. _CT_capsule -> -ct-capsule).</summary>
    private static string Slug(string name) => name["def_".Length..].ToLowerInvariant().Replace('_', '-');

    private static string ItemPath(string name) => "items/" + name["def_".Length..] + ".md";

    /// <summary>For a CT capsule: the production details of the item it carries (its
    /// research level, the component cost, and the output linked to the real item's
    /// page). Null when the payload is not a catalog item (robots, named fits).</summary>
    private static (int Def, string Name, string Url, int? Research, string Cost)? PayloadProduction(
        string name, Dictionary<int, DefRow> defs, Dictionary<string, DefRow> defsByName, Dictionary<int, int> research, Dictionary<int, List<(int Comp, int Amt)>> components)
    {
        var payloadName = name[..^"_CT_capsule".Length];
        if (!defsByName.TryGetValue(payloadName, out var payload) || !IsItem(payload)) return null;
        var cost = components.TryGetValue(payload.Definition, out var comps) && comps.Count > 0
            ? string.Join(", ", comps
                .OrderBy(c => c.Amt == 0 ? 1 : 0)
                .Select(c => defs.TryGetValue(c.Comp, out var cd)
                    ? $"{Md.DisplayName(cd.Name)} ×{c.Amt}"
                    : $"{c.Comp} ×{c.Amt}"))
            : "–";
        // Research level: the payload's own level where it has one, otherwise the
        // level of its prototype variant (_pr rows in itemresearchlevels).
        int? rl = null;
        if (research.TryGetValue(payload.Definition, out var own)) rl = own;
        else if (defsByName.TryGetValue(payloadName + "_pr", out var pr) && research.TryGetValue(pr.Definition, out var prLvl)) rl = prLvl;
        return (payload.Definition, payloadName, "/content/items/" + Slug(payloadName) + "/", rl, cost);
    }

    /// <summary>The standard item page: identity table (definition, tier, health, volume,
    /// mass, category, note), the stats table, and for CT capsules the payload's
    /// production details.</summary>
    private static string ItemPage(DefRow d, string showName, (string Cat, string Sub) cs, List<string> stats,
        (int Def, string Name, string Url, int? Research, string Cost)? production, DefRow? ct,
        List<(ShopPage.Location Loc, int Qty, long Tm, long Ics, long Asi, long Credit, long Uni, double Standing)>? vendors)
    {
        var tier = Md.Tier(d.TierType, d.TierLevel);
        var category = cs.Sub == "General" || cs.Sub == cs.Cat ? cs.Cat : $"{cs.Cat} / {cs.Sub}";
        var description = category + (tier.Length > 0 ? $", tier {tier}" : "");
        var sb = new StringBuilder();
        sb.Append(Md.Header(showName, description, $"entitydefaults definition {d.Definition}, aggregatevalues via aggregatefields"));
        sb.Append($"\n\n# {showName}\n\n");
        var rows = new List<string[]>
        {
            new[] { "Definition", $"`{d.Name}`" },
            new[] { "Tier", tier.Length > 0 ? tier : "–" },
            new[] { "Health", Md.Cell(d.Health) },
            new[] { "Volume", Md.Cell(d.Volume) },
            new[] { "Mass", Md.Cell(d.Mass) },
            new[] { "Category", category },
        };
        if (!string.IsNullOrEmpty(d.Note)) rows.Add(new[] { "Note", d.Note });
        Md.WriteTable(sb, new[] { "", "" }, rows.ToArray());
        sb.Append('\n');
        if (stats.Count > 0)
        {
            sb.Append("## Stats\n\n");
            var srows = stats.Select(s =>
            {
                var i = s.IndexOf('=');
                return new[] { i >= 0 ? s[..i] : s, i >= 0 ? s[(i + 1)..] : "–" };
            }).ToArray();
            Md.WriteTable(sb, new[] { "Field", "Value" }, srows);
        }
        else
        {
            sb.Append("_No stats — this item carries no aggregate values._\n");
        }
        if (production is { } p)
        {
            sb.Append("\n## Production\n\n");
            sb.Append("The item this capsule carries, and how it is produced (see [Recipes](/content/recipes/) for the full list).\n\n");
            var prows = new List<string[]>
            {
                new[] { "Research level", p.Research?.ToString() ?? "–" },
                new[] { "Production cost", p.Cost },
                new[] { "Output", $"[{Md.DisplayName(p.Name)}]({p.Url})" },
            };
            Md.WriteTable(sb, new[] { "", "" }, prows.ToArray());
            sb.Append("\n");
            sb.Append(MermaidDiagram(showName, Md.DisplayName(p.Name), p.Url));
        }
        else if (ct is { } c)
        {
            sb.Append("\n## Transport capsule\n\n");
            sb.Append("A transport capsule (CT) exists for this item — it is how the item moves between players and zones.\n\n");
            sb.Append(MermaidDiagram(showName, Md.DisplayName(c.Name), "/content/items/" + Slug(c.Name) + "/"));
        }
        if (vendors is { Count: > 0 } v)
        {
            sb.Append("\n## Where to buy\n\n");
            sb.Append("Fixed-price vendors that sell this item (see the [Item shop](/content/shop/) for the full catalog).\n\n");
            var header = new[] { "Vendor", "Qty", "TM Coin", "ICS Coin", "ASI Coin", "Credits", "UniCoin", "Standing" };
            var cells = v.Select(x => new[]
            {
                ShopPage.Title(x.Loc),
                x.Qty.ToString(),
                ShopPage.Coin(x.Tm), ShopPage.Coin(x.Ics), ShopPage.Coin(x.Asi),
                x.Credit > 0 ? Md.Cell(x.Credit) : "–",
                ShopPage.Coin(x.Uni),
                x.Standing > 0 ? Md.Cell(x.Standing) : "–",
            }).ToArray();
            var trimmed = ShopPage.TrimColumns(header, cells);
            Md.WriteTable(sb, trimmed.Header, trimmed.Cells);
        }
        sb.Append("\n[All items](/content/items/)\n");
        return sb.ToString();
    }

    /// <summary>Two-node transport diagram: the current page's item (green) points at the
    /// linked item. Rendered client-side by mermaid (see base.html).</summary>
    private static string MermaidDiagram(string currentDisplay, string otherDisplay, string otherUrl)
    {
        static string Q(string s) => s.Replace("\"", "'");
        return $$"""
            ```mermaid
            graph LR
                a["{{Q(currentDisplay)}} (current)"]:::current
                b["{{Q(otherDisplay)}}"]
                a --> b
                click b "{{otherUrl}}" "{{Q(otherDisplay)}}"
                classDef current fill:#2f9e6f,stroke:#1f6f4a,color:#ffffff
            ```
            """;
    }
}
