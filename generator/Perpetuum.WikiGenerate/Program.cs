// wiki-generate — Phase 2 generator for the Open Perpetuum wiki.
//
// Reads the Perpetuum database (read-only) and emits the generated stat pages under
// wiki/content/content/. The output is committed to git; the site build itself never
// needs a database (see wiki/idea.md).
//
// Usage:
//   wiki-generate --connection "<conn string>" --plantrules <dir> --out <dir>
//
//   --connection  SQL Server connection string (or $PERPETUUM_CONNECTIONSTRING)
//   --plantrules  directory containing the plant rule files ($GameRoot/plantrules)
//   --out         output directory (the wiki/content/content folder)
namespace Perpetuum.WikiGenerate;

public static class Program
{
    public static int Main(string[] args)
    {
        string? connection = Environment.GetEnvironmentVariable("PERPETUUM_CONNECTIONSTRING");
        string? plantrulesDir = null, outDir = null, zonesOutDir = null, gbfPath = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--connection": connection = args[++i]; break;
                case "--plantrules": plantrulesDir = args[++i]; break;
                case "--zones-out": zonesOutDir = args[++i]; break;
                case "--out": outDir = args[++i]; break;
                case "--gbf": gbfPath = args[++i]; break;
                case "--help": PrintHelp(); return 0;
                default: Console.Error.WriteLine($"unknown argument: {args[i]}"); PrintHelp(); return 2;
            }
        }
        if (string.IsNullOrEmpty(connection)) { Console.Error.WriteLine("missing --connection (or $PERPETUUM_CONNECTIONSTRING)"); return 2; }
        if (string.IsNullOrEmpty(plantrulesDir) || !Directory.Exists(plantrulesDir)) { Console.Error.WriteLine($"missing/invalid --plantrules dir: {plantrulesDir}"); return 2; }
        if (string.IsNullOrEmpty(outDir)) { Console.Error.WriteLine("missing --out"); return 2; }

        // Client display names (zones, robots, items) come from the client string
        // dictionary inside the GBF asset archive. Optional: without it the names are
        // derived from the internal definition names.
        if (!string.IsNullOrEmpty(gbfPath))
        {
            if (!File.Exists(gbfPath)) { Console.Error.WriteLine($"--gbf file not found: {gbfPath}"); return 2; }
            Console.WriteLine($"loading client dictionary from {gbfPath} ...");
            Md.ClientStrings = Gbf.LoadDictionary(gbfPath);
            Console.WriteLine($"  {Md.ClientStrings.Count} client strings");
        }

        using var db = new Db(connection);

        Console.WriteLine("loading entitydefaults ...");
        var defs = db.Query("SELECT definition, definitionname, attributeflags, categoryflags, options, note, enabled, hidden, volume, mass, health, quantity, tiertype, tierlevel FROM entitydefaults")
            .Select(r => new DefRow(r.Int("definition"), r.Str("definitionname"), r.Lng("attributeflags"), r.Lng("categoryflags"),
                r.Str("options"), r.Str("note"), r.Bit("enabled"), r.Bit("hidden"), r.Dbl("volume"), r.Dbl("mass"), r.Dbl("health"), r.Int("quantity"), r.Int("tiertype"), r.Int("tierlevel")))
            .ToDictionary(d => d.Definition);

        Console.WriteLine("loading aggregate values ...");
        var fields = db.Query("SELECT id, name FROM aggregatefields").ToDictionary(r => r.Int("id"), r => r.Str("name"));
        var statsByDef = new Dictionary<int, List<string>>();
        foreach (var r in db.Query("SELECT definition, field, value FROM aggregatevalues"))
        {
            if (!fields.TryGetValue(r.Int("field"), out var fname)) continue;
            if (!statsByDef.TryGetValue(r.Int("definition"), out var list)) statsByDef[r.Int("definition")] = list = new List<string>();
            list.Add($"{fname}={FormatStat(r.Dbl("value"))}");
        }
        foreach (var kv in statsByDef) kv.Value.Sort(StringComparer.Ordinal);

        Directory.CreateDirectory(outDir);
        // Shop sales are shared by the shop page and the item pages ("Where to buy").
        var shop = ShopPage.Load(db);
        var pages = new List<(string File, string Content)>
        {
            ("ores.md", OresPage.Build(db, defs)),
            ("plants.md", PlantsPage.Build(db, defs, plantrulesDir)),
            ("deployables.md", DeployablesPage.Build(db, defs, statsByDef)),
            ("robots.md", RobotsPage.Build(db, defs, statsByDef)),
            ("extensions.md", ExtensionsPage.Build(db)),

            ("missions.md", MissionsPage.Build(db)),
            ("shop.md", ShopPage.Build(db, shop)),
            ("recipes.md", RecipesPage.Build(db)),
        ("stat-reference.md", StatsReferencePage.Build(db)),
        };
        // The shop catalog is one page per category under shop/.
        foreach (var (_, slug, _) in ShopPage.Categories)
        {
            try { pages.Add((Path.Combine("shop", slug + ".md"), ShopPage.BuildCategoryPage(db, shop, slug))); }
            catch (Exception e) { Console.Error.WriteLine($"shop category {slug} failed: {e.Message}"); }
        }
        // The tech tree is an index + one page per category + one page per node.
        pages.AddRange(TechTreePage.Build(db));
        // The item catalog is one page per item under items/ plus its index.
        pages.AddRange(ItemsPage.Build(db, defs, statsByDef, shop));
        var itemPages = 0;
        foreach (var (file, content) in pages)
        {
            var path = Path.Combine(outDir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            if (file.StartsWith("items/", StringComparison.Ordinal)) { itemPages++; continue; }
            Console.WriteLine($"wrote {file} ({content.Length / 1024} KB)");
        }
        Console.WriteLine($"wrote items/ ({itemPages} pages incl. index)");

        // Zone index lives in the zones/ section, not the content/ section.
        if (!string.IsNullOrEmpty(zonesOutDir))
        {
            Directory.CreateDirectory(zonesOutDir);
            var zoneIndex = ZoneIndexPage.Build(db);
            File.WriteAllText(Path.Combine(zonesOutDir, "zone-index.md"), zoneIndex);
            Console.WriteLine($"wrote zones-out/zone-index.md ({zoneIndex.Length / 1024} KB)");
            var zoneMap = ZonesMapPage.Build(db);
            File.WriteAllText(Path.Combine(zonesOutDir, "map.md"), zoneMap);
            Console.WriteLine($"wrote zones-out/map.md ({zoneMap.Length / 1024} KB)");
        }

        // Section landing page (hand-written body, generated counts).
        var index = BuildIndex(pages);
        // _index.md = the section landing page (Zola section page).
        File.WriteAllText(Path.Combine(outDir, "_index.md"), index);
        Console.WriteLine("wrote _index.md");

        // Client search index (title/description/URL per page) for the lightweight
        // search in wiki/static/search.js. outDir is <wiki>/content/content.
        var wikiRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(outDir)))!;
        SearchIndex.Build(wikiRoot);

        Console.WriteLine("done.");
        return 0;
    }

    private static string BuildIndex(List<(string File, string Content)> pages)
    {
        return $$"""
            ---
            title: "Content"
            description: "Generated stat tables for every content entity: ores, plants, deployables, items, robots."
            ---

            # Content

            Stat tables for every content entity, generated from the live database.

            | Page | Contents |
            |---|---|
            | [Ores](/content/ores/) | All ore types, extraction yields, per-zone node generation |
            | [Plants](/content/plants/) | Every plant species rule (growRate, fertility, spreading, fruit), per-zone fertility |
            | [Deployables](/content/deployables/) | Deployable structures and placeables with stats |
            | [Items](/content/items/) | Full item catalog: modules, armor, ammo, materials, robot components |
            | [Robots](/content/robots/) | Every robot by class and generation, with strengths and weaknesses |
            | [Extensions](/content/extensions/) | Full extension (skill) tree: rank, price, bonus, prerequisites |
            | [Tech tree](/content/techtree/) | Every tech tree node: unlocked item, enabler extension, point prices |
            | [Missions](/content/missions/) | All missions: type, level, duration, reward fee, reward items |
            | [Item shop](/content/shop/) | Vendor catalog per shop location and item category, with the price in every currency |
            | [Recipes](/content/recipes/) | Every craftable item and its components, research level |

            Field meanings (what a stat column actually does) live in [Formats](/formats/).
            """;
    }

    private static string FormatStat(double v) => Md.Num(v);

    private static void PrintHelp() => Console.Error.WriteLine(
        "wiki-generate --connection <cs> --plantrules <dir> --out <dir> [--zones-out <dir>] [--gbf <archive.gbf>]");
}
