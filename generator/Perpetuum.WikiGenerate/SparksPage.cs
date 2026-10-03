namespace Perpetuum.WikiGenerate;

/// <summary>
/// The sparks page (content/features/sparks.md): the hand-written prose plus
/// a generated "Spark families" overview (a compact graph, one box per
/// family) whose boxes link to the per-family card sections below (47 spark
/// cards: unlock cost/standing, switch cost, bundled extension levels).
/// The generated block is marked with &lt;!-- sparkfamilies:generated --&gt; so
/// tools/gen_spark_families.py can rebuild it without a database (it must
/// produce byte-identical output).
/// </summary>
public static class SparksPage
{
    public sealed record SparkFamily(string Key, string Label, string Slug, string Color, string Box, string Unlock, List<SparkRow> Sparks);
    public sealed record SparkRow(string Name, string Label, string Unlock, bool IsPrice, string Switch, bool Default, int Bundle);

    public static string Build(Db db, string familySvg)
    {
        var families = BuildFamilies(db);
        var sparkCount = families.Sum(f => f.Sparks.Count);

        var sb = new StringBuilder();
        sb.Append("""
            ---
            title: "Sparks"
            description: "Sparks are the nanobot manifestation of your agent — passive ability bonuses, authorization, and switching costs."
            weight: 10
            ---

            # Sparks

            A **spark** is a nanobot specialization installed on your character. While it is
            active, its bonuses apply to every robot you pilot — the spark for a faction's
            combat line, for example, raises weapon damage and critical chance level by
            level, while an industrial line raises core capacity and mining yield.

            Each character has a **default spark** given at creation. Beyond that, sparks
            come in families — the basic default lines, per-faction combat/industrial/social
            lines (three levels each), paid Syndicate lines, and special limited lines
            — and a spark's exact bonuses are just a bundle of fixed skill levels you can
            see on the [extensions page](/content/extensions/). The [family overview below](#families)
            jumps straight to each family's sparks.

            ## Unlocking (authorizing) a spark

            Installing a spark you don't own yet requires **unlocking** it first (docked
            only). Depending on the spark, the server checks:

            - a **price** in NIC (the paid lines — e.g. the Syndicate utility sparks and the
              limited special sparks),
            - **standing** with the developer megacorporation (the per-faction lines need a
              minimum standing with that faction's megacorp, built by completing its
              [missions](/features/missions/)),
            - or an **item** taken from your inventory (some special sparks are unlocked
              with a key item).

            Unlocking is one-time: once unlocked, the spark stays in your collection.

            ```mermaid
            stateDiagram-v2
                [*] --> Active: character created (default spark installed)
                Active --> Active: one spark at a time
                Active --> Cooldown: switch (costs NIC, per-spark)
                Cooldown --> Active: after 1 hour (new spark's bonuses apply to all robots)
            ```

            """);

        // Switching rules come BEFORE the family catalog: a reader deciding
        // whether a different spark is worth it needs the cost/cooldown first.
        sb.Append("""
            <a id="switching-sparks"></a>

            ## Switching sparks

            - You can only have **one active spark** at a time; installing another swaps it.
            - Each switch **costs NIC** — the amount is per-spark (the common lines cost
              10k, the paid special lines up to a million).
            - There is a **one-hour cooldown** between switches: the server tracks when your
              current spark was activated and refuses a new one until the minute is over.

            Because switching always costs NIC and takes an hour, pick the spark that
            matches the activity you are spending the most time on, and treat re-
            specializing as an occasional decision rather than a per-session one.

            """);

        sb.Append("<!-- sparkfamilies:generated -->\n");
        sb.Append("<a id=\"families\"></a>\n\n");
        sb.Append("## Spark families\n\n");
        sb.Append($"The {sparkCount} sparks in {families.Count} families at a glance: one box per family " +
                  "(spark count, how the line unlocks), in the order the lines were added " +
                  "(left to right, top to bottom). **Click a box to jump to that family's " +
                  "sparks below.** " +
                  "**Scroll over the diagram to zoom**, drag to pan, and use the ⟲ button to reset.\n\n");
        sb.Append("<div class=\"map-zoom-wrap sparkfam-wrap\">\n");
        sb.Append("<button type=\"button\" class=\"zoommap-reset\" title=\"Reset the zoom\">\u27f2</button>\n");
        var tag = familySvg.IndexOf("<svg ", StringComparison.Ordinal);
        sb.Append(familySvg.Insert(tag + 5, "class=\"zoommap\" "));
        sb.Append("</div>\n\n");

        foreach (var f in families)
        {
            sb.Append($"<a id=\"family-{f.Slug}\"></a>\n\n");
            sb.Append($"### {Escape(f.Label)} ({f.Sparks.Count})\n\n");
            sb.Append($"Unlock: {f.Unlock}. Each switch costs NIC (the amount is per spark, see the cards) " +
                      "and takes a one-hour cooldown — see [Switching sparks](#switching-sparks) above.\n\n");
            sb.Append("<div class=\"ext-cards\">\n");
            foreach (var s in f.Sparks)
            {
                sb.Append("<div class=\"ext-card\">\n");
                sb.Append($"<div class=\"ext-card-name\">{Escape(s.Label)}</div>\n");
                sb.Append($"<div class=\"ext-card-meta\">unlock <span class=\"{(s.IsPrice ? "ext-val-price" : "ext-val-bonus")}\">{Escape(s.Unlock)}</span>" +
                          $" · switch <span class=\"ext-val-price\">{Escape(s.Switch)}</span></div>\n");
                // raw HTML: Zola does not run the markdown parser inside HTML blocks
                sb.Append($"<div class=\"ext-card-prereq\">{(s.Default ? "Default spark — installed at character creation" : $"Bundles {s.Bundle} extension level{(s.Bundle == 1 ? "" : "s")}")}</div>\n");
                sb.Append("</div>\n");
            }
            sb.Append("</div>\n\n");
        }

        // The generated block now extends to the end of the file; the trailer
        // comment below is part of it (tools/gen_spark_families.py preserves
        // everything from the last <!-- through to EOF verbatim).
        sb.Append("""
            <!--
            Written from scratch against the server backend, 2026-09-27:
            RequestHandlers/Sparks/SparkUnlock.cs (unlock rules: price, standing, item),
            RequestHandlers/Sparks/SparkChange.cs + Services/Sparks/SparkHelper.cs
            (60-minute switch cooldown, per-spark change price),
            Services/Sparks/Spark.cs, sparks + sparkextensions tables (47 sparks, per-line
            bonus bundles).
            An earlier version of this page was adapted from the Open Perpetuum
            community wiki (perpetuum.miraheze.org) and has been fully rewritten from
            backend sources; no text was reused.
            -->

            """);
        return sb.ToString();
    }

    /// <summary>Family groups, in the order the players meet the lines: the basic
    /// defaults, the three faction lines, the paid lines. Public so the family
    /// overview SVG (SparksFamilySvg) shows the same families and counts.</summary>
    public static List<SparkFamily> BuildFamilies(Db db)
    {
        return LoadFamilies(db);
    }

    private static List<SparkFamily> LoadFamilies(Db db)
    {
        var sparks = db.Query("""
            SELECT s.id, s.sparkname, s.unlockprice, s.standinglimit, s.changeprice,
                   s.defaultspark, s.displayorder, s.alliancename
            FROM sparks s
            ORDER BY s.displayorder, s.sparkname
            """).ToList();
        var bundle = db.Query("SELECT sparkid, extensionid, extensionlevel FROM sparkextensions")
            .GroupBy(r => r.Int("sparkid"))
            .ToDictionary(g => g.Key, g => g.Count());

        string Family(string name)
        {
            var t = name.StartsWith("spark_") ? name["spark_".Length..] : name;
            var first = t.Split('_')[0];
            return first switch
            {
                "tm" => "tm",
                "ics" => "ics",
                "asi" => "asi",
                "syndicate" => "syndicate",
                "anniversary" or "amazon" or "steam" => "limited",
                _ => "special",
            };
        }
        // (label, slug, color, short box text, long section text)
        (string Label, string Slug, string Color, string Box, string Unlock) Info(string key) => key switch
        {
            "tm" => ("TM (Truhold-Markson)", "tm", "#41d3ff", "TM standing", "standing with the TM megacorporation (2 → 4 → 6 by level)"),
            "ics" => ("ICS", "ics", "#6ee7a0", "ICS standing", "standing with the ICS megacorporation (2 → 4 → 6 by level)"),
            "asi" => ("ASI", "asi", "#f5a05a", "ASI standing", "standing with the ASI megacorporation (2 → 4 → 6 by level)"),
            "syndicate" => ("Syndicate (NIC)", "syndicate", "#a78bfa", "NIC price", "a NIC price (1M per spark)"),
            "limited" => ("Limited", "limited", "#f472b6", "NIC price", "a NIC price (25–50M per spark)"),
            _ => ("Event & special", "special", "#c8d2e0", "free", "nothing — these are the basic default lines"),
        };
        string SparkLabel(string name, string family)
        {
            var t = name.StartsWith("spark_") ? name["spark_".Length..] : name;
            var toks = t.Split('_');
            // the faction line repeats the family in every name (tm Combat Lvl1
            // under "TM (Truhold-Markson)") — drop the leading family token there
            if (family is "tm" or "ics" or "asi" && toks[0] == family) toks = toks.Skip(1).ToArray();
            return string.Join(' ', toks.Select(tok =>
                tok.Length > 3 && tok.StartsWith("lvl", StringComparison.Ordinal) ? $"Lvl{tok[3..]}" :
                // the cryptic 2-letter codes (the basic line: ww, wi, …) stay as-is
                tok.Length <= 2 ? tok :
                char.ToUpperInvariant(tok[0]) + tok[1..]));
        }
        string Num(long v) => v >= 1_000_000 && v % 1_000_000 == 0 ? $"{v / 1_000_000}M"
            : v >= 1_000 && v % 1_000 == 0 ? $"{v / 1_000}k" : v.ToString();

        // Line order as the players meet them: the basic defaults, then the three
        // faction lines, then the paid lines.
        var order = new Dictionary<string, int>
        {
            ["special"] = 0, ["tm"] = 1, ["ics"] = 2, ["asi"] = 3, ["syndicate"] = 4, ["limited"] = 5,
        };
        var groups = new List<SparkFamily>();
        foreach (var key in sparks.Select(s => Family(s.Str("sparkname"))).Distinct().OrderBy(k => order[k]))
        {
            var (label, slug, color, box, unlock) = Info(key);
            var rows = new List<SparkRow>();
            foreach (var s in sparks.Where(s => Family(s.Str("sparkname")) == key))
            {
                var price = s.Dbl("unlockprice");
                var standing = s.Dbl("standinglimit");
                string unlockTxt;
                if (price > 0)
                {
                    unlockTxt = $"{Num((long)price)} NIC";
                }
                else if (standing > 0)
                {
                    var alliance = s.Str("alliancename").Replace("megacorp_", "");
                    unlockTxt = $"standing {(int)standing} with {alliance}";
                }
                else
                {
                    unlockTxt = "no cost";
                }
                rows.Add(new SparkRow(
                    s.Str("sparkname"),
                    SparkLabel(s.Str("sparkname"), key),
                    unlockTxt,
                    price > 0,
                    $"{Num((long)s.Dbl("changeprice"))} NIC",
                    s.Bit("defaultspark"),
                    bundle.GetValueOrDefault(s.Int("id"), 0)));
            }
            groups.Add(new SparkFamily(key, label, slug, color, box, unlock, rows));
        }
        return groups;
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
