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
        string? plantrulesDir = null, outDir = null, zonesOutDir = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--connection": connection = args[++i]; break;
                case "--plantrules": plantrulesDir = args[++i]; break;
                case "--zones-out": zonesOutDir = args[++i]; break;
                case "--out": outDir = args[++i]; break;
                case "--help": PrintHelp(); return 0;
                default: Console.Error.WriteLine($"unknown argument: {args[i]}"); PrintHelp(); return 2;
            }
        }
        if (string.IsNullOrEmpty(connection)) { Console.Error.WriteLine("missing --connection (or $PERPETUUM_CONNECTIONSTRING)"); return 2; }
        if (string.IsNullOrEmpty(plantrulesDir) || !Directory.Exists(plantrulesDir)) { Console.Error.WriteLine($"missing/invalid --plantrules dir: {plantrulesDir}"); return 2; }
        if (string.IsNullOrEmpty(outDir)) { Console.Error.WriteLine("missing --out"); return 2; }

        // Client display names are a static snapshot (ClientNames.cs) of the
        // official client's string dictionary; the names fall back to being
        // derived from the internal definition names where the client has none.

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

        // The wiki root (for static/): outDir is <wiki>/content/content.
        var wikiRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(outDir)))!;

        Directory.CreateDirectory(outDir);
        // Shop sales are shared by the shop page and the item pages ("Where to buy").
        var shop = ShopPage.Load(db);
        // Main-categories overview SVG (static/extensions-categories.svg):
        // also embedded inline in the extensions page (the boxes are clickable
        // links to the per-category sections below).
        var (catSvg, catCount, catEdges) = ExtensionsCategories.Build(db);
        File.WriteAllText(Path.Combine(wikiRoot, "static", "extensions-categories.svg"), catSvg);
        Console.WriteLine($"wrote static/extensions-categories.svg ({catCount} categories, {catEdges} edges)");
        // Recipe data cache (tools/recipes_data.json) for the Python one-shot
        // tools — the DB is the source of truth, the committed JSON is a
        // snapshot of the `components` + `itemresearchlevels` tables.
        WriteRecipesData(wikiRoot, db, defs);
        // Spark family overview (inlined in the sparks page) + the page itself:
        // the hand-written prose plus a generated family graph whose boxes link
        // to the per-family card sections (#family-<slug>).
        var sparkFamilies = SparksPage.BuildFamilies(db);
        var sparkFamilySvg = SparksFamilySvg.Build(
            sparkFamilies.Select(f => new SparkFamilyInfo(f.Label, f.Slug, f.Color, f.Sparks.Count, f.Box)).ToList());
        var pages = new List<(string File, string Content)>
        {
            ("ores.md", OresPage.Build(db, defs).Index),
            ("plants.md", PlantsPage.Build(db, defs, plantrulesDir)),
            ("deployables.md", DeployablesPage.Build(db, defs, statsByDef)),
            ("robots.md", RobotsPage.Build(db, defs, statsByDef)),
            ("extensions.md", ExtensionsPage.Build(db, catSvg, catCount, catEdges)),

            ("missions.md", MissionsPage.Build(db)),
            ("shop.md", ShopPage.Build(db, shop)),
            ("recipes.md", RecipesPage.Build(db, defs)),
            ("stat-reference.md", StatsReferencePage.Build(db)),
        };
        // The sparks page lives in content/features (not the generated
        // content/content tree): the generator owns its whole content now.
        File.WriteAllText(Path.Combine(wikiRoot, "content", "features", "sparks.md"), SparksPage.Build(db, sparkFamilySvg));
        Console.WriteLine("wrote content/features/sparks.md");
        // The shop catalog is one page per category under shop/.
        foreach (var (_, slug, _) in ShopPage.Categories)
        {
            try { pages.Add((Path.Combine("shop", slug + ".md"), ShopPage.BuildCategoryPage(db, shop, slug))); }
            catch (Exception e) { Console.Error.WriteLine($"shop category {slug} failed: {e.Message}"); }
        }
        // The tech tree is an index + one page per category + one page per node.
        pages.AddRange(TechTreePage.Build(db));
        // The item catalog is one page per item under items/ (no index page).
        pages.AddRange(ItemsPage.Build(db, defs, statsByDef, shop));
        // The deployables catalog: one page per deployable under deployables/.
        pages.AddRange(DeployablesPage.BuildPages(db, statsByDef));
        // Remove the previous run's artifacts before writing: the generator
        // overwrites in place, and without this a definition removed from the
        // DB would leave a ghost page behind (and in the client search index).
        // items/, shop/ and techtree/ are owned entirely by the generator; the
        // hand-written sections (features, zones, formats, menu) are never touched.
        foreach (var dir in new[] { "items", "shop", "techtree", "ores", "deployables" })
        {
            var p = Path.Combine(outDir, dir);
            if (Directory.Exists(p)) Directory.Delete(p, true);
        }
        foreach (var file in pages.Where(p => !p.File.Contains('/')).Select(p => p.File).Distinct())
        {
            var p = Path.Combine(outDir, file);
            if (File.Exists(p)) File.Delete(p);
        }
        File.Delete(Path.Combine(outDir, "_index.md"));

        var itemPages = 0;
        var orePages = 0;
        foreach (var (file, content) in pages)
        {
            var path = Path.Combine(outDir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            if (file.StartsWith("items/", StringComparison.Ordinal)) { itemPages++; continue; }
            Console.WriteLine($"wrote {file} ({content.Length / 1024} KB)");
        }
        // One page per ore type (the ores index above links to each of them).
        foreach (var (file, content) in OresPage.Build(db, defs).OrePages)
        {
            var path = Path.Combine(outDir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            orePages++;
        }
        Console.WriteLine($"wrote items/ ({itemPages} pages incl. index)");
        Console.WriteLine($"wrote ores/ ({orePages} ore pages)");

        // The zone pages live in the zones/ section, not the content/ section:
        // one page per zone (the three hand-written worked examples are skipped).
        // Hand-written files in zones/ the generator must never touch — any other
        // zone_*.md is owned by ZonePages and is cleaned up before writing.
        var ZonesKeep = new HashSet<string>(StringComparer.Ordinal)
        {
            "_index.md", "generation.md", "map.md", "protection.md", "zone-index.md",
            "zone_tm.md", "zone_asi.md", "zone_gamma_z106.md",
        };
        if (!string.IsNullOrEmpty(zonesOutDir))
        {
            Directory.CreateDirectory(zonesOutDir);
            if (Directory.Exists(zonesOutDir))
            {
                foreach (var f in Directory.GetFiles(zonesOutDir))
                {
                    var fn = Path.GetFileName(f);
                    if (fn.StartsWith("zone_", StringComparison.Ordinal) && fn.EndsWith(".md", StringComparison.Ordinal)
                        && !ZonesKeep.Contains(fn))
                        File.Delete(f);
                }
            }
            var zoneIndex = ZoneIndexPage.Build(db);
            File.WriteAllText(Path.Combine(zonesOutDir, "zone-index.md"), zoneIndex);
            Console.WriteLine($"wrote zones-out/zone-index.md ({zoneIndex.Length / 1024} KB)");
            // Zone meta for tools/gen_zone_teleport_maps.py (name -> slug,
            // sizes, ids): the zone index is a card grid now, so the tool
            // reads this TSV instead of parsing the old index table.
            var meta = db.Query("SELECT id, name, width, height FROM zones ORDER BY id")
                .GroupBy(r => r.Str("name"))
                .Select(g => g.First())
                .Select(r => {
                    var name = r.Str("name");
                    var disp = Md.ClientStrings.TryGetValue(name, out var d) && d != name ? d : name;
                    var slug = name.ToLowerInvariant().Replace("_", "-");
                    var id = r.Int("id");
                    var zw = r.Int("width");
                    var zh = r.Int("height");
                    return $"{name}\t{disp}\t{slug}\t{id}\t{zw}\t{zh}";
                });
            File.WriteAllText(Path.Combine(wikiRoot, "static", "zonemaps", "meta.tsv"), string.Join("\n", meta) + "\n");
            Console.WriteLine($"wrote static/zonemaps/meta.tsv ({meta.Count()} zones)");
            var zoneMap = ZonesMapPage.Build(db);
            File.WriteAllText(Path.Combine(zonesOutDir, "map.md"), zoneMap);
            Console.WriteLine($"wrote zones-out/map.md ({zoneMap.Length / 1024} KB)");
            // Per-family zone-listing pages (alpha.md / beta.md / gamma.md).
            foreach (var (file, content) in ZonesMapPage.FamilyPages(db))
                File.WriteAllText(Path.Combine(zonesOutDir, file), content);
            Console.WriteLine("wrote zones-out family pages (alpha/beta/gamma)");
            var (zonePages, zoneMaps) = ZonePages.BuildAll(db);
            foreach (var (file, content) in zonePages)
                File.WriteAllText(Path.Combine(zonesOutDir, file), content);
            Console.WriteLine($"wrote zones-out zone pages ({zonePages.Count})");
            // Per-zone teleport maps (SVG) served from static/zonemaps/.
            var mapsDir = Path.Combine(wikiRoot, "static", "zonemaps");
            Directory.CreateDirectory(mapsDir);
            foreach (var f in Directory.GetFiles(mapsDir, "*.svg")) File.Delete(f);
            foreach (var (name, svg) in zoneMaps)
                File.WriteAllText(Path.Combine(mapsDir, name.ToLowerInvariant().Replace("_", "-") + ".svg"), svg);
            Console.WriteLine($"wrote static/zonemaps ({zoneMaps.Count} svgs)");
        }

        // Section landing page (hand-written body, generated counts).
        var index = BuildIndex(pages);
        // _index.md = the section landing page (Zola section page).
        File.WriteAllText(Path.Combine(outDir, "_index.md"), index);
        Console.WriteLine("wrote _index.md");

        // Client search index (title/description/URL per page) for the lightweight
        // search in wiki/static/search.js. outDir is <wiki>/content/content.
        SearchIndex.Build(wikiRoot);

        Console.WriteLine("done.");
        return 0;
    }

    private static string BuildIndex(List<(string File, string Content)> pages)
    {
        return $$"""
            ---
            title: "Content"
            description: "Generated stat tables for every content entity: ores, plants, deployables, robots. "
            ---

            # Content

            Stat tables for every content entity, generated from the live database.

            | Page | Contents |
            |---|---|
            | [Ores](/content/ores/) | All ore types and extraction yields; per-zone node parameters on each ore page |
            | [Plants](/content/plants/) | Every plant species rule (growRate, fertility, spreading, fruit), per-zone fertility |
            | [Deployables](/content/deployables/) | Deployable structures and placeables with stats |
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

    /// <summary>
    /// tools/recipes_data.json: { recipes: { def: { components: [[def, qty]],
    /// research: "N" | "–" }, items, ores } — consumed by the Python tools
    /// (gen_production_pages.py reads the recipes part when the old table is
    /// gone). Component order matches the pages (definition-name order).
    /// </summary>
    private static void WriteRecipesData(string wikiRoot, Db db, Dictionary<int, DefRow> defs)
    {
        var comps = db.Query("SELECT definition, componentdefinition, componentamount FROM components");
        var research = db.Query("SELECT definition, MAX(researchlevel) AS lvl FROM itemresearchlevels GROUP BY definition")
            .ToDictionary(r => r.Int("definition"), r => r.Int("lvl"));
        string NameOf(int def) => defs.TryGetValue(def, out var d) ? d.Name : def.ToString();
        var byResult = comps
            .GroupBy(c => c.Int("definition"))
            .OrderBy(g => NameOf(g.Key), StringComparer.Ordinal)
            .ToDictionary(
                g => NameOf(g.Key),
                g => g.OrderBy(c => NameOf(c.Int("componentdefinition")), StringComparer.Ordinal)
                      .Select(c => (Name: NameOf(c.Int("componentdefinition")), Amt: c.Int("componentamount")))
                      .ToList());

        string LabelOf(DefRow d)
        {
            if (d.Name.EndsWith("_bot")) return Md.DisplayName(d.Name, forceDerived: true) + " Bot";
            if ((d.CatFlags & Flags.CfOre) == Flags.CfOre)
                return Md.DisplayName(d.Name.StartsWith("def_", StringComparison.Ordinal) ? d.Name["def_".Length..] : d.Name);
            return Md.DisplayName(d.Name);
        }
        var sb = new StringBuilder();
        sb.Append("{\n \"recipes\": {\n");
        var first = true;
        foreach (var (res, parts) in byResult)
        {
            if (!first) sb.Append(",\n");
            first = false;
            var rl = research.TryGetValue(
                defs.First(kv => kv.Value.Name == res).Key, out var lvl) ? lvl.ToString() : "\u2013";
            sb.Append($"  \"{res}\": {{\"components\": [");
            for (var i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"[\"{parts[i].Name}\", {parts[i].Amt}]");
            }
            sb.Append($"], \"research\": \"{rl}\"}}");
        }
        sb.Append("\n },\n \"names\": {\n");
        first = true;
        foreach (var kv in defs.OrderBy(kv => kv.Value.Name, StringComparer.Ordinal))
        {
            if (!first) sb.Append(",\n");
            first = false;
            sb.Append($"  \"{kv.Value.Name}\": \"{EscapeJson(LabelOf(kv.Value))}\"");
        }
        sb.Append("\n },\n");
        var path = Path.Combine(wikiRoot, "tools", "recipes_data.json");
        // The items/ores sections are maintained by tools/gen_production_pages.py
        // (it rewrites this file from the committed item pages) — carry them
        // over instead of blanking them.
        var items = "{}";
        var ores = "{}";
        if (File.Exists(path))
        {
            try
            {
                var old = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
                if (old?["items"] is System.Text.Json.Nodes.JsonObject { } i && i.Count > 0) items = i.ToJsonString();
                if (old?["ores"] is System.Text.Json.Nodes.JsonObject { } o && o.Count > 0) ores = o.ToJsonString();
            }
            catch (Exception e) { Console.WriteLine($"[warn] recipes_data.json carry-over skipped: {e.Message}"); }
        }
        sb.Append(" \"items\": " + items + ",\n \"ores\": " + ores + "\n}\n");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
        Console.WriteLine($"wrote tools/recipes_data.json ({byResult.Count} recipes)");
    }

    private static string EscapeJson(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    private static void PrintHelp() => Console.Error.WriteLine(
        "wiki-generate --connection <cs> --plantrules <dir> --out <dir> [--zones-out <dir>]");
}
