namespace Perpetuum.WikiGenerate;

public sealed record DefRow(int Definition, string Name, long AttribFlags, long CatFlags,
    string Options, string Note, bool Enabled, bool Hidden, double Volume, double Mass,
    double Health, int Qty, int TierType, int? TierLevel);

public sealed record FieldRow(int Id, string Name, string Unit, double Multiplier, double Offset, int Digits, bool? MoreIsBetter);

public sealed record MineralRow(int Idx, string Name, int Definition, int Amount, int ExtractionType, bool EnablerRequired, int? GeoScanDocument);

public sealed record MineralConfigRow(int ZoneId, int MaterialType, int MaxNodes, int MaxTilesPerNode, int TotalAmountPerNode, double MinThreshold);

public sealed record ZoneRow(int Id, string Name, int Fertility, int PlantRuleSet, int ZoneType, bool Protected, bool Enabled, double PlantAltitudeScale, int SparkCost, int Width, int Height);

public sealed record PlantRuleRefRow(int Idx, string File, int RuleSet);

public sealed record ResearchRow(int Definition, int ResearchLevel, int? CalibrationProgram, bool Enabled);

public sealed record TemplateRow(string Name, string Description);

/// <summary>Category/attribute flag masks (verified against src/Perpetuum.ExportedTypes and DB flag tables).</summary>
public static class Flags
{
    // categoryflags (mask values from categoryFlags table / CategoryFlags.cs)
    public const long CfRobots = 1;
    public const long CfRobotEquipment = 15;
    public const long CfAmmo = 10;
    public const long CfMaterial = 20;
    public const long CfRobotComponents = 80;
    public const long CfArmorEquipment = 271;
    public const long CfRobotHead = 336;
    public const long CfRobotChassis = 592;
    public const long CfRobotLeg = 848;
    public const long CfRobotEnhancements = 3087;
    public const long CfDeployableStructure = 2456;
    public const long CfOre = 131348;
    public const long CfPlantSeed = 920;
    public const long CfMaterialScanResult = 912;

    // attributeflags: DB table stores the BIT POSITION; mask = 1 << position
    public const long AtDeployable = 1L << 23;   // "deployable": item that can be placed in the terrain
    public const long AtActiveModule = 1L << 4;
    public const long AtPassiveModule = 1L << 19;
}
