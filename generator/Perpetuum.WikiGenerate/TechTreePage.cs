using System.Text;

namespace Perpetuum.WikiGenerate;

/// <summary>
/// The tech tree as a page tree: an index, one page per research category
/// (techtreegroups) with its diagram, and one detail page per node (658).
/// Files land under content/techtree/ (index), techtree/groups/ and techtree/nodes/.
/// </summary>
public static class TechTreePage
{
    /// <summary>Internal group name -> player-facing title.</summary>
    private static (string Title, string Blurb) GroupInfo(string name) => name switch
    {
        "nuimqol" => ("Nuimqol (faction)", "Research for the Nuimqol rebel faction: its named weapons and the modules that fight alongside them."),
        "pelistal" => ("Pelistal (faction)", "Research for the Pelistal domain faction: its named weapons and the modules that fight alongside them."),
        "thelodica" => ("Thelodica (faction)", "Research for the Thelodica Clan faction: its named weapons and the modules that fight alongside them."),
        "common1" => ("Common (first set)", "The first set of standard (non-faction) modules available to every player."),
        "common2" => ("Common (second set)", "The second set of standard modules, including the named energy-transfer lines."),
        "pbs" => ("PBS structures", "Power base station capsules and construction modules — the buildings of open-world corporate play."),
        "indy" => ("Industrial", "Industrial research: reactor boosters and the modules that keep production lines moving."),
        _ => (char.ToUpperInvariant(name[0]) + name[1..], ""),
    };

    public static List<(string File, string Content)> Build(Db db)
    {
        var defs = db.Query("SELECT definition, definitionname, enabled, hidden, categoryflags FROM entitydefaults")
            .ToDictionary(r => r.Int("definition"), r => (r.Str("definitionname"), r.Bit("enabled"), r.Bit("hidden"), r.Lng("categoryflags")));
        var exts = db.Query("SELECT extensionid, extensionname FROM extensions")
            .ToDictionary(r => r.Int("extensionid"), r => r.Str("extensionname"));
        var groups = db.Query("SELECT id, name FROM techtreegroups").ToDictionary(r => r.Int("id"), r => r.Str("name"));
        var pointTypes = db.Query("SELECT id, name FROM techtreepointtypes").ToDictionary(r => r.Int("id"), r => r.Str("name"));

        var prices = db.Query("SELECT definition, pointtype, amount FROM techtreenodeprices")
            .GroupBy(r => r.Int("definition"))
            .ToDictionary(g => g.Key, g => g
                .Where(p => pointTypes.TryGetValue(p.Int("pointtype"), out _))
                .OrderBy(p => pointTypes[p.Int("pointtype")])
                .Select(p => (Type: pointTypes[p.Int("pointtype")], Amt: p.Int("amount")))
                .ToList());

        var nodes = db.Query("""
            SELECT childdefinition, parentdefinition, groupID, enablerextensionid
            FROM techtree ORDER BY groupID, childdefinition
            """).ToList();

        var children = nodes.GroupBy(n => n.Int("parentdefinition"))
            .ToDictionary(g => g.Key, g => g.Select(n => n.Int("childdefinition")).OrderBy(d => d).ToList());

        string ItemLink(int def)
        {
            if (!defs.TryGetValue(def, out var d)) return null;
            var (name, enabled, hidden, catFlags) = d;
            if (!enabled || hidden) return null;
            if (name.StartsWith("def_npc_")) return null; // NPC unit fits: no catalog page
            if (name.EndsWith("_bot"))
                // spectator/tutorial bots are not listed on the robots page
                return name.Contains("spectator") || name.Contains("tutorial") ? "/content/robots/" : Md.RobotAnchorUrl(name);
            if (name.EndsWith("_bot_pr")) return "/content/robots/";
            if ((catFlags & Flags.CfOre) == Flags.CfOre) return null;
            if ((catFlags & Flags.CfDeployableStructure) == Flags.CfDeployableStructure) return null;
            return "/content/items/" + name["def_".Length..].ToLowerInvariant().Replace('_', '-') + "/";
        }
        /// <summary>Node page URL: the item slug, without the "_bot" suffix (robots
        /// live on the robot catalog, and the item slugs elsewhere drop the suffix).</summary>
        string NodeUrl(int def)
        {
            var name = defs.TryGetValue(def, out var dd) ? dd.Item1 : "def_" + def;
            if (name.EndsWith("_bot")) name = name[..^4];
            return "/content/techtree/nodes/" + name["def_".Length..].ToLowerInvariant().Replace('_', '-') + "/";
        }
        string ItemCell(int def)
        {
            var name = defs.TryGetValue(def, out var d2) ? d2.Item1 : "def_" + def;
            var display = Md.DisplayName(name);
            var url = ItemLink(def);
            return url is null ? display : $"[{display}]({url})";
        }
        /// <summary>Display text with any markdown link syntax removed.</summary>
        string Label(int def)
        {
            var text = ItemCell(def);
            var open = text.IndexOf('[', StringComparison.Ordinal);
            var close = text.IndexOf(']', StringComparison.Ordinal);
            return open >= 0 && close > open ? text[(open + 1)..close] : text;
        }
        string PointStr(int def) => prices.TryGetValue(def, out var ps)
            ? string.Join("; ", ps.Select(p => $"{p.Type}={Md.Num(p.Amt)}"))
            : "free";

        // ---- index ----
        var byGroup = nodes.GroupBy(n => n.Int("groupID")).ToDictionary(g => g.Key, g => g.ToList());
        var idx = new StringBuilder();
        idx.Append(Md.Header("Tech tree", "The research tree: every category, and every node with the item it unlocks, its prerequisites and its point prices.",
            "techtree, techtreegroups, techtreenodeprices, techtreepointtypes (joined to entitydefaults and extensions)"));
        idx.Append("\n\n# Tech tree\n\n");
        idx.Append("Tech tree nodes unlock items by spending **research points** (earned from kernels — see " +
                   "[Research](/features/research/) in the features section). A node can only be unlocked when its " +
                   "**parent** node is unlocked and the **enabler extension** is learned. Point types: " +
                   string.Join(", ", pointTypes.Values.OrderBy(x => x)) + ".\n\n");
        idx.Append("## Categories\n\n");
        idx.Append("Each category is its own page with the full tree and one page per node.\n\n");
        var irows = byGroup.Keys.OrderBy(k => k).Select(gid =>
        {
            var gname = groups[gid];
            var (title, blurb) = GroupInfo(gname);
            var roots = byGroup[gid].Count(n => n.Int("parentdefinition") == 0);
            return new[]
            {
                $"[ {title} ](/content/techtree/groups/{gname.Replace('_', '-').ToLowerInvariant()}/)",
                byGroup[gid].Count.ToString(),
                roots.ToString(),
                blurb,
            };
        }).ToArray();
        Md.WriteTable(idx, new[] { "Category", "Nodes", "Root lines", "What it unlocks" }, irows);
        idx.Append('\n');

        var pages = new List<(string File, string Content)> { ("techtree/_index.md", idx.ToString()) };

        // ---- one page per category ----
        foreach (var gid in byGroup.Keys.OrderBy(k => k))
        {
            var gname = groups[gid];
            var (title, blurb) = GroupInfo(gname);
            var gnodes = byGroup[gid];
            var slug = gname.Replace('_', '-').ToLowerInvariant();

            var sb = new StringBuilder();
            sb.Append(Md.Header($"Tech tree — {title}", $"{title} research category: {gnodes.Count} nodes.",
                "techtree + techtreenodeprices (group " + gid + ")"));
            sb.Append($"\n\n# {title}\n\n");
            sb.Append(blurb + "\n\n");
            sb.Append("[Tech tree](/content/techtree/) → " + title + ". The category is split into its " +
                      "**research lines** — one per top-level item (the chips above jump to each line's " +
                      "graph). Every node in a graph links to its detail page (prerequisites, what it " +
                      "unlocks next, point prices).\n\n");

            // the lines: every top-level node (no parent) starts one research
            // line; the line = the root plus everything that descends from it.
            // One graph per line keeps each diagram small enough to read
            // without zooming (the old single category-wide graph was a wall).
            // childdef -> rootdef (root = a node with no parent)
            var lineOf = new Dictionary<int, int>();
            void Walk(int c, int root)
            {
                lineOf[c] = root;
                if (children.TryGetValue(c, out var k)) foreach (var x in k) Walk(x, root);
            }
            var roots = gnodes.Where(n => n.Int("parentdefinition") == 0)
                .OrderBy(n => n.Int("childdefinition")).ToList();
            foreach (var rn in roots) Walk(rn.Int("childdefinition"), rn.Int("childdefinition"));
            var byRoot = new Dictionary<int, List<Dictionary<string, object?>>>();
            foreach (var n in gnodes)
            {
                var c = n.Int("childdefinition");
                if (!lineOf.TryGetValue(c, out var rt)) continue; // defensive: disconnected node
                if (!byRoot.TryGetValue(rt, out var l)) byRoot[rt] = l = new List<Dictionary<string, object?>>();
                l.Add(n);
            }

            // chip index (missions style): one chip per line, root-name based.
            // RAW HTML — Zola does not run the markdown parser inside HTML
            // blocks, so a [label](#anchor) here would render as literal text.
            sb.Append("<div class=\"tt-index\">\n");
            foreach (var rn in roots)
            {
                var r = rn.Int("childdefinition");
                var rname = Label(r);
                var chip = rname.StartsWith("Standard ", StringComparison.Ordinal) ? rname["Standard ".Length..] : rname;
                if (chip.Length > 1) chip = char.ToUpperInvariant(chip[0]) + chip[1..];
                sb.Append($"<a href=\"#line-{r}\"><span class=\"tt-chip\">{chip}</span></a>&ensp;\n");
            }
            sb.Append("</div>\n\n");

            foreach (var rn in roots)
            {
                var r = rn.Int("childdefinition");
                var lns = byRoot.TryGetValue(r, out var l2) ? l2 : new List<Dictionary<string, object?>>();
                var rname = Label(r);
                sb.Append($"<a id=\"line-{r}\"></a>\n\n## {rname}\n\n");
                sb.Append($"**{lns.Count} node{(lns.Count == 1 ? "" : "s")}** — everything that descends from " +
                          $"[{rname}]({NodeUrl(r)}).\n\n");

                var ordered = lns.OrderBy(x => x.Int("childdefinition")).ToList();
                var m = new StringBuilder();
                m.AppendLine("```mermaid");
                m.AppendLine("graph TD");
                foreach (var n in ordered)
                {
                    var c = n.Int("childdefinition");
                    var label = Md.DisplayName(defs.TryGetValue(c, out var d3) ? d3.Item1 : "def_" + c).Replace("\"", "'");
                    m.AppendLine($"    n{c}[\"{label}\"]");
                }
                foreach (var n in ordered)
                {
                    var c = n.Int("childdefinition");
                    var p = n.Int("parentdefinition");
                    if (p != 0) m.AppendLine($"    n{p} --> n{c}");
                }
                foreach (var n in ordered)
                {
                    var c = n.Int("childdefinition");
                    m.AppendLine($"    click n{c} \"{NodeUrl(c)}\" \"{Label(c)}\"");
                }
                m.AppendLine("```");
                sb.Append(m.ToString());
                sb.Append("\n");
            }

            sb.Append("## Nodes (all)\n\n");
            var rows = gnodes
                .OrderBy(n => n.Int("parentdefinition"))
                .ThenBy(n => n.Int("childdefinition"))
                .Select(n =>
                {
                    var c = n.Int("childdefinition");
                    var p = n.Int("parentdefinition");
                    return new[]
                    {
                        $"[{Label(c)}]({NodeUrl(c)})",
                        p == 0 ? "– (root)" : $"[{Label(p)}]({NodeUrl(p)})",
                        n.Int("enablerextensionid") != 0 && exts.TryGetValue(n.Int("enablerextensionid"), out var e)
                            ? $"[{e}](/content/extensions/)" : "–",
                        PointStr(c),
                    };
                }).ToArray();
            Md.WriteTable(sb, new[] { "Unlocks item", "Parent node", "Enabler extension", "Point prices" }, rows);
            sb.Append('\n');

            pages.Add((Path.Combine("techtree", "groups", slug + ".md"), sb.ToString()));
        }

        // ---- one detail page per node ----
        foreach (var n in nodes)
        {
            var c = n.Int("childdefinition");
            var p = n.Int("parentdefinition");
            var gid = n.Int("groupID");
            var gname = groups[gid];
            var (gtitle, _) = GroupInfo(gname);
            var gslug = gname.Replace('_', '-').ToLowerInvariant();
            var name = defs.TryGetValue(c, out var d4) ? d4.Item1 : "def_" + c;
            var display = Md.DisplayName(name);

            var sb = new StringBuilder();
            sb.Append(Md.Header(display + " — tech tree node", $"Research node that unlocks {display} ({gtitle} category).",
                "techtree + techtreenodeprices (childdefinition " + c + ")"));
            sb.Append($"\n\n# {display} (research node)\n\n");
            sb.Append($"This node of the [{gtitle}](/content/techtree/groups/{gslug}/) research tree unlocks " +
                      $"{ItemCell(c)}.\n\n");
            string LinkNode(int def)
            {
                var label = Label(def);
                return $"[{label}]({NodeUrl(def)})";
            }
            var kids = children.TryGetValue(c, out var k2) ? k2 : new List<int>();
            var enabler = n.Int("enablerextensionid");
            var rows = new List<string[]>
            {
                new[] { "Category", $"[{gtitle}](/content/techtree/groups/{gslug}/)" },
                new[] { "Unlocks", ItemCell(c) },
                new[] { "Parent node", p == 0 ? "– (root line)" : LinkNode(p) },
                new[] { "Unlocks next", kids.Count > 0
                    ? string.Join(", ", kids.Select(k => LinkNode(k)))
                    : "– (end of line)" },
                new[] { "Enabler extension", enabler != 0 && exts.TryGetValue(enabler, out var e2)
                    ? $"[{e2}](/content/extensions/)" : "–" },
                new[] { "Point prices", PointStr(c) },
            };
            Md.WriteTable(sb, new[] { "", "" }, rows.ToArray());
            sb.Append("\n[All tech tree nodes](/content/techtree/)\n");

            var nodeFile = name.EndsWith("_bot") ? name[..^4] : name;
            pages.Add((Path.Combine("techtree", "nodes", nodeFile["def_".Length..] + ".md"), sb.ToString()));
        }

        return pages;
    }
}
