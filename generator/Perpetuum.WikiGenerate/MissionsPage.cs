namespace Perpetuum.WikiGenerate;

public static class MissionsPage
{
    public static string Build(Db db)
    {
        var defs = db.Query("SELECT definition, definitionname FROM entitydefaults")
            .ToDictionary(r => r.Int("definition"), r => r.Str("definitionname"));
        var types = db.Query("SELECT id, name, category, categoryvalue FROM missiontypes")
            .ToDictionary(r => r.Int("id"), r => (name: r.Str("name"), category: r.Str("category"), value: r.Int("categoryvalue")));

        var rewards = db.Query("SELECT missionid, definition, quantity, probability FROM missionrewards")
            .GroupBy(r => r.Int("missionid"))
            .ToDictionary(
                g => g.Key,
                g => string.Join("; ", g
                    .Select(r => {
                        var name = defs.TryGetValue(r.Int("definition"), out var dn) ? dn : r.Int("definition").ToString();
                        return $"{name} ×{Md.Num(r.Int("quantity"))} ({r.Int("probability")})";
                    })
                    .OrderBy(x => x)));

        var missions = db.Query("""
            SELECT m.id, m.name, m.missiontype, m.missionlevel, m.durationminutes, m.periodminutes,
                   m.rewardfee, m.difficultyreward, m.difficultymultiplier, m.listable, m.alwaysenabled, m.isunique
            FROM missions m ORDER BY m.missionlevel, m.name
            """).ToList();

        var sb = new StringBuilder();
        sb.Append(Md.Header("Missions", "Every mission: type, level, duration, reward fee, and reward items.",
            "missions, missiontypes, missionrewards (joined to entitydefaults)"));
        sb.Append("\n\n# Missions\n\n");
        sb.Append("All mission definitions (see [Missions](/features/missions/) in the features section). " +
                  "**Reward fee** is the credit payout; **Rewards** are the item drops (quantity and drop probability).\n\n");

        var rows = missions.Select(m =>
        {
            types.TryGetValue(m.Int("missiontype"), out var t);
            return new[]
            {
                m.Str("name"),
                t.name,
                t.category,
                m.Int("missionlevel").ToString(),
                m.Int("durationminutes").ToString(),
                m.Int("periodminutes").ToString(),
                Md.Num(m.Dbl("rewardfee")),
                Md.Num(m.Dbl("difficultyreward")),
                Md.Cell(m.Dbl("difficultymultiplier")),
                m.Bit("listable") ? "yes" : "no",
                rewards.TryGetValue(m.Int("id"), out var rw) ? rw : "–"
            };
        }).ToArray();
        Md.WriteTable(sb, new[]
        {
            "Mission", "Type", "Category", "Level", "Duration (min)", "Period (min)", "Reward fee", "Difficulty reward", "Difficulty ×", "Listable", "Rewards"
        }, rows);

        return sb.ToString();
    }
}
