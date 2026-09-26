namespace Perpetuum.WikiGenerate;

public static class DeployablesPage
{
    public static string Build(Db db, Dictionary<int, DefRow> defs, Dictionary<int, List<string>> statsByDef)
    {
        var rows = db.Query("SELECT definition, definitionname, attributeflags, categoryflags, options, note, enabled, volume, mass, health, tiertype, tierlevel FROM entitydefaults ORDER BY definitionname")
            .Where(r => r.HasFlag("categoryflags", Flags.CfDeployableStructure) || (r.Lng("attributeflags") & Flags.AtDeployable) == Flags.AtDeployable)
            .Select(r => new DefRow(r.Int("definition"), r.Str("definitionname"), r.Lng("attributeflags"), r.Lng("categoryflags"),
                r.Str("options"), r.Str("note"), r.Bit("enabled"), r.Bit("hidden"), r.Dbl("volume"), r.Dbl("mass"), r.Dbl("health"), r.Int("quantity"), r.Int("tiertype"), r.Int("tierlevel")))
            .ToList();

        var sb = new StringBuilder();
        sb.Append(Md.Header("Deployables", "Deployable structures and placeable items, with their stats.",
            "entitydefaults (cf_deployable_structure / deployable attribute flag), aggregatevalues"));
        sb.Append("\n\n# Deployables\n\n");
        sb.Append("Deployables are items that can be **placed in the terrain** to act on the zone — PBS structures, effect suppliers, repairers, turrets, and similar. Placing one is a corporation/privilege-gated action (see [Power base stations](/features/pbs/) in the features section). How deployables are identified, paired capsule↔object, and which stat fields drive them is in [Deployable fields](/formats/deployable-fields/).\n\n");

        var table = rows.Select(d => new[]
        {
            d.Name,
            Md.Tier(d.TierType, d.TierLevel),
            Md.Cell(d.Health),
            Md.Cell(d.Volume),
            Md.Cell(d.Mass),
            statsByDef.TryGetValue(d.Definition, out var s) && s.Count > 0 ? string.Join("; ", s) : "–",
            d.Note
        }).ToArray();
        Md.WriteTable(sb, new[] { "Definition", "Tier", "Health", "Volume", "Mass", "Stats", "Note" }, table);

        return sb.ToString();
    }
}
