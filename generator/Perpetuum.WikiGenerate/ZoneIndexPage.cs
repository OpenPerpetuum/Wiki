namespace Perpetuum.WikiGenerate;

public static class ZoneIndexPage
{
    // ZoneType enum (src/Perpetuum/Zones/ZoneType.cs)
    private static readonly string[] ZoneTypes = { "Undefined", "PvE", "PvP", "Training", "Stronghold" };

    public static string Build(Db db)
    {
        var species = db.Query("SELECT rulesetid, COUNT(*) AS n FROM plantrules GROUP BY rulesetid")
            .ToDictionary(r => r.Int("rulesetid"), r => r.Int("n"));

        var mineralStats = db.Query("""
            SELECT zoneId, COUNT(*) AS minerals, SUM(maxnodes) AS totalNodes, SUM(totalamountpernode) AS totalAmount
            FROM mineralconfigs GROUP BY zoneId
            """).ToDictionary(r => r.Int("zoneId"), r => (minerals: r.Int("minerals"), totalNodes: r.Lng("totalNodes"), totalAmount: r.Lng("totalAmount")));

        var zones = db.Query("""
            SELECT id, name, x, y, width, height, zonetype, protected, fertility, plantruleset,
                   terraformable, pbsTechLimit, timeLimitMinutes, PlantsGrowthTimerOverrideMin
            FROM zones ORDER BY id
            """).ToList();

        var sb = new StringBuilder();
        sb.Append(Md.Header("Zone index", "Every zone: type, protection, fertility, size, plant species count, and ore configuration.",
            "zones, mineralconfigs, plantrules"));
        sb.Append("\n\n# Zone index\n\n");
        sb.Append("All zones on the server. **Type**: PvE (peaceful), PvP (open combat), Training, or Stronghold. " +
                  "**Fertility** is the zone's plant coverage target (percent of ground tiles). **Ore nodes** is the " +
                  "sum of `maxnodes` across the zone's `mineralconfigs` rows — the total number of ore nodes the zone " +
                  "maintains per material type combined. Zones without an ore configuration (–) have no ore layers " +
                  "(arenas, training zones, strongholds, gamma tc zones). See [Generation](/zones/generation/) for how these " +
                  "numbers are used. The binary layer files that make up a zone are documented in [Zone files](/formats/zone-files/).\n\n");

        // Display name from the client string dictionary where the client has one
        // (gamma zones and similar have none and keep their internal name).
        string ZoneName(string n) => Md.ClientStrings.TryGetValue(n, out var d) && d != n ? $"{d} ({n})" : n;

        var rows = zones.Select(z =>
        {
            var type = z.Int("zonetype") >= 0 && z.Int("zonetype") < ZoneTypes.Length ? ZoneTypes[z.Int("zonetype")] : "?";
            var speciesCount = species.TryGetValue(z.Int("plantruleset"), out var n) ? n.ToString() : "–";
            mineralStats.TryGetValue(z.Int("id"), out var ms);
            return new[]
            {
                z.Int("id").ToString(),
                ZoneName(z.Str("name")),
                type,
                z.Bit("protected") ? "protected" : "open",
                z.Int("fertility").ToString(),
                $"{z.Int("width")}×{z.Int("height")}",
                speciesCount,
                ms.minerals > 0 ? ms.minerals.ToString() : "–",
                ms.minerals > 0 ? ms.totalNodes.ToString() : "–"
            };
        }).ToArray();
        Md.WriteTable(sb, new[]
        {
            "Id", "Name", "Type", "Protection", "Fertility", "Size", "Plant species", "Ore materials", "Ore nodes (total)"
        }, rows);

        return sb.ToString();
    }
}
