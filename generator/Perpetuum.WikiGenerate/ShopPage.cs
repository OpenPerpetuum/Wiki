namespace Perpetuum.WikiGenerate;

/// <summary>One itemshop row (a vendor preset selling one item).</summary>
public record ShopSale(int Preset, int Def, int Qty, long Tm, long Ics, long Asi, long Credit, long Uni, double Standing);

public static class ShopPage
{
    public sealed record Location(string? Zone, string Name, string Short, string Area, string Note, int[] Presets);

    /// <summary>Shop locations, derived from itemshoppresets, ordered by protection
    /// area (starter → main → beta, then the coin-exchange outposts). The main
    /// galaxy presets come in PvE/PvP variants; preset 3 (dev_test) is internal
    /// tooling and is not shown.</summary>
    public static readonly Location[] Locations =
    {
        new("zone_TM", "New Virginia (zone_TM)", "TM zone", "Starter area",
            "The New Virginia (TM) galaxy's shop — sold at bases in its PvE and PvP zones.", new[] { 1, 6 }),
        new("zone_ICS", "Attalica (zone_ICS)", "ICS zone", "Main",
            "The Attalica (ICS) galaxy's shop — sold at bases in its PvE and PvP zones.", new[] { 4, 7 }),
        new(null, "Attalica outpost", "Attalica outpost", "Main",
            "Vendor at the Attalica (ICS) outpost.", new[] { 13 }),
        new("zone_ASI", "Daoden (zone_ASI)", "ASI zone", "Beta",
            "The Daoden (ASI) galaxy's shop — sold at bases in its PvE and PvP zones.", new[] { 5, 8 }),
        new(null, "Daoden outpost", "Daoden outpost", "Beta",
            "Vendor at the Daoden (ASI) outpost — the largest shop catalog.", new[] { 14 }),
        new(null, "Outpost: Bellicha", "Bellicha", "Outpost",
            "Mission coin exchange outpost (sells the other galaxies' mission coins and the universal coin).", new[] { 10 }),
        new(null, "Outpost: Cadavaria", "Cadavaria", "Outpost",
            "Mission coin exchange outpost (sells the other galaxies' mission coins and the universal coin).", new[] { 11 }),
        new(null, "Outpost: Lenworth", "Lenworth", "Outpost",
            "Mission coin exchange outpost (sells the other galaxies' mission coins and the universal coin).", new[] { 12 }),
    };

    public static List<ShopSale> Load(Db db) => db.Query("""
        SELECT s.presetid, s.targetdefinition, s.targetamount, s.tmcoin, s.icscoin, s.asicoin,
               s.credit, s.unicoin, s.standing
        FROM itemshop s ORDER BY s.presetid, s.targetdefinition
        """).Select(r => new ShopSale(r.Int("presetid"), r.Int("targetdefinition"), r.Int("targetamount"),
        r.Lng("tmcoin"), r.Lng("icscoin"), r.Lng("asicoin"), r.Lng("credit"), r.Lng("unicoin"), r.Dbl("standing")))
        .ToList();

    /// <summary>Best terms of one item at one location (lowest price per currency,
    /// lowest qty, lowest standing) across the location's presets.</summary>
    public static (int Qty, long Tm, long Ics, long Asi, long Credit, long Uni, double Standing) BestFor(
        List<ShopSale> sales, Location loc, int def)
    {
        var rows = sales.Where(s => s.Def == def && loc.Presets.Contains(s.Preset)).ToList();
        if (rows.Count == 0) return (0, 0, 0, 0, 0, 0, 0);
        return (rows.Min(r => r.Qty),
            Min(rows, r => r.Tm), Min(rows, r => r.Ics), Min(rows, r => r.Asi),
            Min(rows, r => r.Credit), Min(rows, r => r.Uni), rows.Min(r => r.Standing));
    }

    private static long Min(List<ShopSale> rows, Func<ShopSale, long> f)
    {
        var v = rows.Select(f).Where(x => x > 0).ToList();
        return v.Count > 0 ? v.Min() : 0;
    }

    public static string Coin(long v) => v > 0 ? v.ToString() : "–";

    /// <summary>Drop columns (after the first two) that carry no value in the table.</summary>
    public static (string[] Header, string[][] Cells) TrimColumns(string[] header, string[][] cells)
    {
        var keep = Enumerable.Range(0, header.Length)
            .Where(i => i <= 1 || cells.Any(c => c[i] != "–"))
            .ToArray();
        return (keep.Select(i => header[i]).ToArray(),
                cells.Select(c => keep.Select(i => c[i]).ToArray()).ToArray());
    }

    // Item categories shown inside the catalog, in this order.
    // Classification is by name shape (CT-capsule variants are classified by their
    // payload) plus the module equipment category flag — the raw flags do not
    // separate paint/coins/eggs etc.
    private static readonly (string Name, Func<string, long, bool> Match)[] Categories =
    {
        ("Ammo",          (n, f) => n.StartsWith("def_ammo_")),
        ("Bots",          (n, f) => n.EndsWith("_bot")),
        ("Paint",         (n, f) => n.StartsWith("def_paint_")),
        ("Modules & equipment", (n, f) => (f & Flags.CfRobotEquipment) == Flags.CfRobotEquipment),
        ("Remote commands", (n, f) => n.EndsWith("_remote_command")),
        ("EP boosters",   (n, f) => n.Contains("boost")),
        ("Mission coins", (n, f) => n.Contains("mission_coin")),
        ("Teleports",     (n, f) => n.Contains("teleport")),
        ("NPC eggs",      (n, f) => n.Contains("npc_egg")),
        ("SAP items",     (n, f) => n.Contains("sap_item")),
        ("Other",         (n, f) => true),
    };

    public static string Build(Db db, List<ShopSale> sales)
    {
        var defs = db.Query("SELECT definition, definitionname, enabled, hidden FROM entitydefaults")
            .ToDictionary(r => r.Int("definition"), r => (r.Str("definitionname"), r.Bit("enabled"), r.Bit("hidden")));
        var cats = db.Query("SELECT definition, categoryflags FROM entitydefaults")
            .ToDictionary(r => r.Int("definition"), r => r.Lng("categoryflags"));
        var defByName = defs.ToDictionary(kv => kv.Value.Item1, kv => kv.Key);

        // CT-capsule items are classified by their payload (name + category flags).
        (string Name, long Flags) Payload(int def)
        {
            var dn = defs.TryGetValue(def, out var n) ? n.Item1 : $"def_{def}";
            var payload = dn.EndsWith("_CT_capsule") ? dn[..^11] : dn;
            return (payload, defByName.TryGetValue(payload, out var p) ? cats.GetValueOrDefault(p, 0) : cats.GetValueOrDefault(def, 0));
        }

        string? ItemLink(int def)
        {
            if (!defs.TryGetValue(def, out var d)) return null;
            var (name, enabled, hidden) = d;
            if (!enabled || hidden) return null;
            if (name.EndsWith("_bot")) return "/content/robots/";
            if (name.StartsWith("def_npc_") || name.EndsWith("_bot_pr")) return null;
            var flags = cats.GetValueOrDefault(def, 0);
            if ((flags & Flags.CfOre) == Flags.CfOre) return null;
            if ((flags & Flags.CfDeployableStructure) == Flags.CfDeployableStructure) return null;
            return "/content/items/" + name["def_".Length..].ToLowerInvariant().Replace('_', '-') + "/";
        }

        string ItemCell(int def)
        {
            var name = defs.TryGetValue(def, out var dd) ? dd.Item1 : $"def_{def}";
            var display = Md.DisplayName(name);
            var url = ItemLink(def);
            return url is null ? display : $"[{display}]({url})";
        }

        var allDefs = sales.Select(s => s.Def).Distinct().OrderBy(d => d).ToList();

        var sb = new StringBuilder();
        sb.Append(Md.Header("Item shop", "The fixed-price vendor catalog: what each vendor sells, at what price, and in which protection area.",
            "itemshop + itemshoppresets (joined to entitydefaults)"));
        sb.Append("\n\n# Item shop\n\n");
        sb.Append("Fixed-price vendor items, separate from the player-driven [market](/features/market/). " +
                  "The same item can be sold at several vendors — the catalog below lists every item once, " +
                  "with the best price across all vendors; each [item page](/content/items/) lists every vendor " +
                  "that sells it (robots link to the [robot catalog](/content/robots/)).\n\n");
        sb.Append("**Currencies** — **TM Coin / ICS Coin / ASI Coin**: the per-galaxy shop currency " +
                  "(each shop sells for all three coins where offered); **Credits**: the common currency; " +
                  "**UniCoin**: a premium currency.\n\n");

        // ---- vendors overview, ordered by protection area ----
        sb.Append("## Vendors\n\n");
        var vendorHeader = new[] { "Area", "Vendor", "Sold where", "Items" };
        var vendorCells = Locations
            .Select(loc =>
            {
                var count = allDefs.Count(def => BestFor(sales, loc, def).Qty > 0);
                return new[]
                {
                    loc.Area,
                    Title(loc),
                    loc.Zone is null ? "Outpost vendor" : "Bases in the zone's PvE and PvP zones",
                    count > 0 ? count.ToString() : "–",
                };
            })
            .ToArray();
        Md.WriteTable(sb, vendorHeader, vendorCells);
        sb.Append('\n');

        // ---- catalog: every item once, grouped by category ----
        sb.Append("## Catalog\n\n");
        var byCategory = allDefs
            .GroupBy(def => Categories.First(c => c.Match(Payload(def).Name, Payload(def).Flags)).Name)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var (cat, _) in Categories)
        {
            if (!byCategory.TryGetValue(cat, out var catDefs) || catDefs.Count == 0) continue;
            sb.Append($"### {cat} ({catDefs.Count})\n\n");
            var header = new[] { "Item", "Qty", "TM Coin", "ICS Coin", "ASI Coin", "Credits", "UniCoin", "Vendors" };
            var cells = catDefs.Select(def =>
            {
                var selling = Locations.Where(loc => BestFor(sales, loc, def).Qty > 0).ToList();
                var best = selling
                    .Select(loc => BestFor(sales, loc, def))
                    .ToList();
                return new[]
                {
                    ItemCell(def),
                    best.Min(b => b.Qty).ToString(),
                    Coin(best.Min(b => b.Tm)), Coin(best.Min(b => b.Ics)), Coin(best.Min(b => b.Asi)),
                    Coin(best.Min(b => b.Credit)), Coin(best.Min(b => b.Uni)),
                    string.Join(" · ", selling.Select(loc => loc.Short)),
                };
            }).ToArray();
            var trimmed = TrimColumns(header, cells);
            Md.WriteTable(sb, trimmed.Header, trimmed.Cells);
            sb.Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>The per-vendor rows of one item (one row per location that sells it,
    /// PvE/PvP presets merged to the best terms). Empty when the item is not shop stock.</summary>
    public static List<(Location Loc, int Qty, long Tm, long Ics, long Asi, long Credit, long Uni, double Standing)>
        VendorsFor(List<ShopSale> sales, int def) =>
        Locations
            .Select(loc => (Loc: loc, Best: BestFor(sales, loc, def)))
            .Where(x => x.Best.Qty > 0)
            .Select(x => (x.Loc, x.Best.Qty, x.Best.Tm, x.Best.Ics, x.Best.Asi, x.Best.Credit, x.Best.Uni, x.Best.Standing))
            .ToList();

    /// <summary>"New Virginia (zone_TM)" for the galaxy shops, plain name elsewhere.
    /// Falls back to the plain location name when the client dictionary is not loaded.</summary>
    public static string Title(Location l)
    {
        if (l.Zone is null) return l.Name;
        var n = Md.ZoneName(l.Zone);
        return n == l.Zone ? n : $"{n} ({l.Zone})";
    }
}
