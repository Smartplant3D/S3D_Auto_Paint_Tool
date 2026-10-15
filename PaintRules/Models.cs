namespace PaintRules;

public enum MaterialCategory { CS, LAS, SS, AL, NI, CU, GALV, HDPE, CPVC, OTHER, UNKNOWN }
public enum SpecStatus { Active, Project, Obsolete }
public enum InsulationCondition { Exposed, Insulated, H3, PP, Review }
public enum PaintStatus { OK, AUTO_SET, WOULD_SET, MISMATCH, REVIEW, NOT_APPLICABLE, ERROR }

public sealed record PipeRunData(
    string Oid, string Name, string PipelineName, string? Spec,
    decimal? MaxOperatingTemp, decimal? OperatingTemp, decimal? DesignTemp,
    string? InsulationPurpose, decimal? InsulationThickness, string? InsulationSpec,
    string? ExistingPaintCode,
    IReadOnlyDictionary<string, bool?>? SpecialFlags = null);

public sealed record SpecMaterial(string Spec, MaterialCategory Category, string MaterialGroup,
    SpecStatus Status, string Source, string Revision, string Notes);

public sealed record PaintingRule(string Project, MaterialCategory Category, string InsulationCondition,
    decimal? TempMin, decimal? TempMax, int BandRank, string PaintCode, bool AutoAssignable, string Notes);

public sealed record AppSettings(string Project, string MappingVersion, string PaintRuleVersion,
    bool UseLowestApplicableBand, InsulationCondition PersonnelProtectionAs, bool DryRun,
    bool OverwriteExistingPaintCode);

public sealed record EvaluationResult(PipeRunData Run, MaterialCategory MaterialCategory,
    string MaterialGroup, string SpecStatus, InsulationCondition InsulationCondition,
    IReadOnlyList<string> CandidatePaintCodes, string? ResultPaintCode, PaintStatus Status, string Reason);

public sealed record RuleConfig(AppSettings Settings,
    IReadOnlyDictionary<string, SpecMaterial> SpecMaterials,
    IReadOnlyList<PaintingRule> PaintingRules,
    IReadOnlyDictionary<string, InsulationCondition> InsulationMapping);
