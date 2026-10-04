namespace Perpetuum.WikiGenerate;

/// <summary>
/// The zone index: every zone with an inter-zone teleport point, as family
/// sections of zone cards (the same design as the family pages — alpha/beta/
/// gamma). Zones without any TP connection to another zone (strongholds,
/// the PvP arena, dead-end gate islands) are left off, exactly like the
/// world map.
/// </summary>
public static class ZoneIndexPage
{
    public static string Build(Db db)
    {
        var zones = db.Query("SELECT id, name, zonetype, x, y, width, height, protected, terraformable, enabled FROM zones ORDER BY id")
            .Select(r => new ZonesMapPage.Zone(r.Str("name"), r.Int("zonetype"), r.Dbl("x"), r.Dbl("y"),
                r.Int("width"), r.Int("height"), r.Bit("protected"), r.Bit("terraformable")))
            .ToList();

        // The zones table can hold several rows with the same name (sentinel
        // rows at 50000/51000) — one entry per zone name, lowest id first.
        var byName = zones.GroupBy(z => z.Name).ToDictionary(g => g.Key, g => g.First());
        var all = byName.Values.ToList();

        // The same connection set as the world map: a zone stays on the
        // index only if it is a source or a target of an active inter-zone
        // teleport point (teleportdescriptions).
        var names = db.Query("""
            SELECT DISTINCT zs.name FROM teleportdescriptions td
            JOIN zones zs ON zs.id = td.sourcezone
            JOIN zones zd ON zd.id = td.targetzone
            WHERE td.sourcezone <> td.targetzone AND td.active = 1
            UNION
            SELECT DISTINCT zd.name FROM teleportdescriptions td
            JOIN zones zs ON zs.id = td.sourcezone
            JOIN zones zd ON zd.id = td.targetzone
            WHERE td.sourcezone <> td.targetzone AND td.active = 1
            """).Select(r => r.Str("name")).ToList();
        var connected = new HashSet<string>(names, StringComparer.Ordinal);
        var listed = new HashSet<string>(all.Where(z => connected.Contains(z.Name)).Select(z => z.Name), StringComparer.Ordinal);

        var groups = ZonesMapPage.Groups(all);

        var sb = new StringBuilder();
        sb.Append(Md.Header("Zone index", "Every zone with a teleport connection to another zone, grouped by family.",
            "zones, teleportdescriptions"));
        sb.Append("\n\n# Zone index\n\n");
        sb.Append($"All {listed.Count} zones that have at least one teleport point to ANOTHER zone, grouped by family " +
                  "(the same grouping as the [world map](/zones/map/)) — the card links to the zone's page. Zones without any " +
                  "inter-zone teleport point (the strongholds, the PvP arena, dead-end gate islands) are left off; they are " +
                  "still reachable from the world map's gate lines. The per-family pages — [Alpha](/zones/alpha/) · " +
                  "[Beta](/zones/beta/) · [Gamma](/zones/gamma/) — list every zone of the family regardless of TPs.\n\n");
        foreach (var g in groups)
        {
            var zs = g.Zones.Where(z => listed.Contains(z.Name)).ToList();
            if (zs.Count == 0) continue;
            sb.Append($"<a id=\"{g.Id}\"></a>\n\n## {g.Label}\n\n");
            if (!ZonesMapPage.IsTier(g)) sb.Append(g.Blurb + "\n\n");
            sb.Append(ZonesMapPage.Cards(zs));
        }
        sb.Append("\n[World map](/zones/map/) · [Protection levels](/zones/protection/)\n");
        return sb.ToString();
    }
}
