namespace Perpetuum.WikiGenerate;

/// <summary>The missions catalog, one section per mission type. Each type lists
/// its tiers as cards (level, reward fee, duration, and one box per reward item).
/// A tier is a (type, level) group: the named mission definitions are the zone
/// and sub-variants of that tier, so the card shows the fee range across them
/// and the union of their reward items. NULL-name rows are unlisted placeholders
/// and are excluded.</summary>
public static class MissionsPage
{
    private sealed record Tier(int Type, int Level, int Variants, double FeeMin, double FeeMax,
        int DurationMin, int DurationMax, List<Reward> Rewards);

    private sealed record Reward(string Name, string Url, int QMin, int QMax, int Prob);

    public static string Build(Db db)
    {
        var defs = db.Query("SELECT definition, definitionname FROM entitydefaults")
            .ToDictionary(r => r.Int("definition"), r => r.Str("definitionname"));
        var types = db.Query("SELECT id, name, category, categoryvalue FROM missiontypes")
            .ToDictionary(r => r.Int("id"), r => (name: r.Str("name"), category: r.Str("category"), value: r.Int("categoryvalue")));

        var rewards = db.Query("SELECT missionid, definition, quantity, probability FROM missionrewards").ToList();
        var missions = db.Query("""
            SELECT m.id, m.name, m.missiontype, m.missionlevel, m.durationminutes, m.rewardfee
            FROM missions m WHERE m.name IS NOT NULL
            """).ToList();

        // Group into tiers: (type, level). Level -1 is the tutorial step (shown as "Tutorial").
        var tiers = new Dictionary<(int, int), Tier>();
        void AddTier(int type, int level, int variants, double fee, int duration)
        {
            if (!tiers.TryGetValue((type, level), out var t))
                tiers[(type, level)] = t = new Tier(type, level, 0, fee, fee, duration, duration, new());
            else
            {
                var (qmin, qmax) = t.Rewards.Count == 0
                    ? (int.MaxValue, 0)
                    : (t.Rewards.Min(r => r.QMin), t.Rewards.Max(r => r.QMax));
                t = new Tier(type, level, t.Variants + 1, Math.Min(t.FeeMin, fee), Math.Max(t.FeeMax, fee),
                    Math.Min(t.DurationMin, duration), Math.Max(t.DurationMax, duration), t.Rewards);
            }
            t = new Tier(type, level, t.Variants + 1, t.FeeMin, t.FeeMax, t.DurationMin, t.DurationMax, t.Rewards);
        }
        foreach (var m in missions)
        {
            var (type, level) = (m.Int("missiontype"), m.Int("missionlevel"));
            AddTier(type, level, 1, m.Dbl("rewardfee"), m.Int("durationminutes"));
            // Union of rewards for this tier, keyed by definition.
            var t = tiers[(type, level)];
            foreach (var r in rewards.Where(r => r.Int("missionid") == m.Int("id")))
            {
                var dn = defs.TryGetValue(r.Int("definition"), out var n) ? n : r.Int("definition").ToString();
                var url = ItemUrl(dn);
                var qty = r.Int("quantity");
                var prob = r.Int("probability");
                var idx = t.Rewards.FindIndex(x => x.Name == dn);
                if (idx >= 0)
                {
                    var old = t.Rewards[idx];
                    t.Rewards[idx] = old with { QMin = Math.Min(old.QMin, qty), QMax = Math.Max(old.QMax, qty), Prob = Math.Min(old.Prob, prob) };
                }
                else
                {
                    t.Rewards.Add(new Reward(dn, url, qty, qty, prob));
                }
            }
        }
        tiers = tiers.ToDictionary(kv => kv.Key, kv => kv.Value with { Rewards = kv.Value.Rewards.OrderBy(r => r.Name).ToList() });

        var sb = new StringBuilder();
        sb.Append(Md.Header("Missions", "Every mission by type and tier: reward fee, duration, and the reward items.",
            "missions, missiontypes, missionrewards (joined to entitydefaults)"));
        sb.Append("\n\n# Missions\n\n");
        sb.Append("One section per **mission type**; each tier is a card. **Reward fee** is the credit payout " +
                  "(a range when a tier has several zone variants); **rewards** are the item drops " +
                  "(quantity range and drop probability, 100 = guaranteed). See [Missions](/features/missions/) " +
                  "in the features section for how missions work.\n\n");
        var ordered = types.OrderBy(kv => kv.Value.value).ThenBy(kv => kv.Value.name, StringComparer.Ordinal)
            .Where(kv => tiers.Keys.Any(k => k.Item1 == kv.Key))
            .ToList();
        sb.Append("<div class=\"mission-index\">\n");
        foreach (var (tid, tname) in ordered)
            sb.Append($"[<span class=\"mission-type-chip\">{TypeTitle(tname.name)}</span>](#{TypeAnchor(tname.name)})&ensp;\n");
        sb.Append("</div>\n\n");

        foreach (var (tid, tname) in ordered)
        {
            var list = tiers.Where(kv => kv.Key.Item1 == tid).OrderBy(kv => kv.Key.Item2).Select(kv => kv.Value).ToList();
            if (list.Count == 0) continue;
            var title = TypeTitle(tname.name);
            sb.Append($"<a id=\"{TypeAnchor(tname.name)}\"></a>\n\n## {title}\n\n");
            var cat = CategoryTitle(tname.category);
            if (!cat.Equals(title, StringComparison.OrdinalIgnoreCase))
                sb.Append($"<span class=\"mission-category\">{cat}</span>\n\n");
            sb.Append("<div class=\"mission-cards\">\n");
            foreach (var t in list)
                Card(sb, t);
            sb.Append("</div>\n\n");
        }
        return sb.ToString();
    }

    private static void Card(StringBuilder sb, Tier t)
    {
        var level = t.Level < 0 ? "Tutorial" : $"Level {t.Level}";
        var fee = t.FeeMin == t.FeeMax ? Md.Num(t.FeeMin) : $"{Md.Num(t.FeeMin)} – {Md.Num(t.FeeMax)}";
        var dur = t.DurationMin == t.DurationMax ? $"{t.DurationMin} min" : $"{t.DurationMin}–{t.DurationMax} min";
        sb.Append("<div class=\"mission-card\">\n");
        sb.Append("<div class=\"mission-card-head\">\n");
        sb.Append("<span class=\"icon-mask mission-card-icon\" role=\"img\" aria-label=\"mission icon\"></span>\n");
        sb.Append($"<div><div class=\"mission-card-title\">{level}</div>\n");
        sb.Append($"<div class=\"mission-card-sub\">Reward fee {fee}</div>\n");
        sb.Append($"<div class=\"mission-card-sub\">Duration {dur}</div></div>\n");
        sb.Append("</div>\n");
        if (t.Rewards.Count > 0)
        {
            sb.Append("<div class=\"mission-card-rewards\">\n");
            foreach (var r in t.Rewards)
            {
                var qty = r.QMin == r.QMax ? Md.Num(r.QMin) : $"{Md.Num(r.QMin)}–{Md.Num(r.QMax)}";
                // raw <a>, not a markdown link: this line is a raw HTML block
                // (it starts with <div>), and markdown is not parsed inside
                // raw HTML blocks — [x](y) would render as literal text.
                var name = r.Url is null ? Md.DisplayName(r.Name) : $"<a href=\"{r.Url}\">{Md.DisplayName(r.Name)}</a>";
                var prob = r.Prob < 100 ? $" ({r.Prob}%)" : "";
                sb.Append($"<div class=\"reward-box\"><span class=\"icon-mask reward-box-icon\" role=\"img\" aria-label=\"item icon\"></span> {name} ×{qty}{prob}</div>\n");
            }
            sb.Append("</div>\n");
        }
        sb.Append("</div>\n");
    }

    /// <summary>Display names the fused compound type names would not split on
    /// ("killandfetch" has no camelCase boundary).</summary>
    private static readonly Dictionary<string, string> TypeNames = new(StringComparer.Ordinal)
    {
        ["missiontype_killandfetch"] = "Kill and fetch",
        ["missiontype_onlykill"] = "Kill only",
        ["missiontype_huntthescout"] = "Hunt the scout",
        ["missiontype_scanandloot"] = "Scan and loot",
        ["missiontype_defendandmine"] = "Defend and mine",
    };

    /// <summary>missiontype_courier -> "Courier"; missiontype_scan_robot -> "Scan robot".
    /// CamelCase runs are split into words ("KillAndFetch" -> "Kill And Fetch").</summary>
    private static string TypeTitle(string defname)
    {
        if (TypeNames.TryGetValue(defname, out var o))
            return o;
        var s = defname.StartsWith("missiontype_") ? defname["missiontype_".Length..] : defname;
        var words = System.Text.RegularExpressions.Regex
            .Matches(s, "[a-z]+|[A-Z][a-z]*")
            .Select(m => m.Value)
            .ToList();
        if (words.Count == 0) return s;
        var joined = string.Join(' ', words.Select(w => w.ToLowerInvariant()));
        return char.ToUpperInvariant(joined[0]) + joined[1..];
    }

    /// <summary>missioncategory_industrial -> "Industrial".</summary>
    private static string CategoryTitle(string defname)
    {
        var s = defname.StartsWith("missioncategory_") ? defname["missioncategory_".Length..] : defname;
        s = s.Replace('_', ' ').Trim();
        return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }

    private static string TypeAnchor(string defname)
        => Md.Slug(TypeTitle(defname));

    /// <summary>Item catalog link for a reward definition, or null when the item
    /// has no catalog page (robots, ores, deployables, hidden defs).</summary>
    private static string ItemUrl(string defname)
    {
        if (defname.EndsWith("_bot")) return null;
        if (defname.StartsWith("def_npc_")) return null;
        var slug = defname["def_".Length..].ToLowerInvariant().Replace('_', '-');
        return "/content/items/" + slug + "/";
    }
}
