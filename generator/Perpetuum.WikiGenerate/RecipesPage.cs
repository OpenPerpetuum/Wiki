namespace Perpetuum.WikiGenerate;

/// <summary>
/// The recipes catalog as category sections of item cards (the same layout as
/// the mission reward cards: .mission-cards / .mission-card / .reward-box).
/// Mirrors the Python tool tools/gen_recipes_cards.py — keep the two in sync.
/// </summary>
public static class RecipesPage
{
    // Top-level category order (mirrors ItemsPage.Categories); products that
    // are not catalog items (bots, internal robot parts) fall into "Other".
    private static readonly string[] CatOrder =
    {
        "Modules", "Ammo", "Robot parts", "Materials", "Production",
        "Mission items", "Artifacts", "Decorations", "Special & other",
    };

    public static string Build(Db db, Dictionary<int, DefRow> defs)
    {
        var byName = defs.Values.ToDictionary(d => d.Name);

        var comps = db.Query("SELECT definition, componentdefinition, componentamount FROM components").ToList();
        var grouped = comps
            .GroupBy(c => c.Int("definition"))
            .Select(g => (Def: g.Key,
                          Parts: g.OrderBy(c => c.Int("componentdefinition"))
                                  .Select(c => (Name: c.Int("componentdefinition"), Amt: c.Int("componentamount")))
                                  .ToList()))
            .OrderBy(g => g.Def)
            .ToList();

        var research = db.Query("SELECT definition, MAX(researchlevel) AS lvl FROM itemresearchlevels GROUP BY definition")
            .ToDictionary(r => r.Int("definition"), r => r.Int("lvl"));

        // What a card (title or component row) can link to.
        bool IsCatalog(int def, out DefRow row)
        {
            if (!defs.TryGetValue(def, out var d)) { row = null!; return false; }
            row = d;
            return ItemsPage.IsItem(d);
        }
        string TitleHtml(int def)
            => IsCatalog(def, out var t)
                ? $"<a href=\"/content/items/{ItemsPage.Slug(t.Name)}/\">{Escape(Md.DisplayName(t.Name))}</a>"
                : Escape(defs.TryGetValue(def, out var d2) ? Md.DisplayName(d2.Name) : def.ToString());
        string CompHtml(int def)
        {
            if (IsCatalog(def, out var t))
                return $"<a href=\"/content/items/{ItemsPage.Slug(t.Name)}/\">{Escape(Md.DisplayName(t.Name))}</a>";
            if (defs.TryGetValue(def, out var d) && (d.CatFlags & Flags.CfOre) == Flags.CfOre)
                return $"<a href=\"/content/ores/{ItemsPage.Slug(d.Name)}/\">{Escape(Md.DisplayName(d.Name))}</a>";
            return Escape(defs.TryGetValue(def, out var d3) ? Md.DisplayName(d3.Name) : def.ToString());
        }
        (string Top, string Sub) CatOf(int def)
        {
            if (defs.TryGetValue(def, out var d) && ItemsPage.IsItem(d))
            {
                var (cat, sub) = ItemsPage.Classify(d, byName);
                return (CatOrder.Contains(cat) ? cat : "Other", sub);
            }
            return ("Other", "");
        }

        // Group the craftable items by the category of the product itself.
        var groups = new Dictionary<(string Top, string Sub), List<int>>();
        foreach (var g in grouped)
        {
            var key = CatOf(g.Def);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<int>();
            list.Add(g.Def);
        }
        int TopRank(string t) => CatOrder.Contains(t) ? CatOrder.IndexOf(t) : CatOrder.Length;
        var order = groups
            .OrderBy(kv => TopRank(kv.Key.Top))
            .ThenBy(kv => kv.Key.Top == "Other")
            .ThenBy(kv => kv.Key.Sub, StringComparer.Ordinal)
            .ToList();
        var tops = order.Select(kv => kv.Key.Top).Distinct().ToList();

        var sb = new StringBuilder();
        sb.Append(Md.Header("Recipes", "Every craftable item, grouped by category, with its components and research level.",
            "components, itemresearchlevels (joined to entitydefaults)"));
        sb.Append("\n\n# Recipes\n\n");
        sb.Append("Component requirements for every item that is assembled from other items, grouped by category " +
                  "(see [Production](/features/production/) in the features section). **Research level** is the maximum " +
                  "research level the item has — higher levels change yield/calibration, not the component list. " +
                  "Each item has its own page with its stats, the items it is a component of, and the vendors that sell it.\n\n");
        // Real anchors, not markdown links: Zola does not run the markdown
        // parser inside an HTML block.
        sb.Append("<div class=\"mission-index\">\n" +
                  string.Join(" ", tops.Select(t => $"<a href=\"#{Anchor(t)}\"><span class=\"mission-type-chip\">{Escape(t)}</span></a>&ensp;")) +
                  "\n</div>\n");

        var lastTop = "";
        foreach (var (top, sub) in order.Select(kv => kv.Key))
        {
            var list = groups[(top, sub)];
            if (top != lastTop)
            {
                lastTop = top;
                sb.Append($"\n## {top}\n");
            }
            if (sub.Length > 0)
                sb.Append($"\n### {sub} ({list.Count})\n");
            sb.Append("\n<div class=\"mission-cards\">\n");
            foreach (var def in list.OrderBy(x => x, StringComparer.Ordinal))
            {
                var parts = grouped.Single(g => g.Def == def).Parts;
                sb.Append("<div class=\"mission-card\">\n");
                sb.Append("<div class=\"mission-card-head\">\n");
                sb.Append("<span class=\"icon-mask mission-card-icon\" role=\"img\" aria-label=\"item icon\"></span>\n");
                sb.Append($"<div><div class=\"mission-card-title\">{TitleHtml(def)}</div>");
                if (research.TryGetValue(def, out var lvl) && lvl > 0)
                    sb.Append($"<div class=\"mission-card-sub\">Research level {lvl}</div>");
                sb.Append("</div>\n</div>\n");
                sb.Append("<div class=\"mission-card-rewards\">\n");
                foreach (var (name, amt) in parts)
                    sb.Append($"<div class=\"reward-box\"><span class=\"icon-mask reward-box-icon\" role=\"img\" " +
                              $"aria-label=\"item icon\"></span> {CompHtml(name)} ×{amt}</div>\n");
                sb.Append("</div>\n</div>\n");
            }
            sb.Append("</div>\n");
        }
        return sb.ToString();
    }

    /// <summary>Heading anchor (same scheme Zola uses for the generated headings).</summary>
    private static string Anchor(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        var t = sb.ToString().Trim('-');
        return t.Replace("--", "-");
    }

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
