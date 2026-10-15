namespace PaintRules;

public interface IStdCorrosionLogic
{
    string? ReviewReason(PipeRunData run, MaterialCategory category, InsulationCondition insulation);
}

public sealed class ReviewOnSpecialConditions : IStdCorrosionLogic
{
    public string? ReviewReason(PipeRunData run, MaterialCategory category, InsulationCondition insulation)
    {
        var flags = run.SpecialFlags;
        if (flags is null || flags.Count == 0) return null;
        var trueFlags = flags.Where(x => x.Value == true).Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (trueFlags.Length > 0) return "STD-ENG-MS012 special condition: " + string.Join(", ", trueFlags);
        if (insulation is InsulationCondition.Insulated or InsulationCondition.H3 &&
            category is MaterialCategory.CS or MaterialCategory.LAS or MaterialCategory.SS)
        {
            var unknown = flags.Where(x => x.Value is null).Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (unknown.Length > 0) return "STD-ENG-MS012 special condition unknown: " + string.Join(", ", unknown);
        }
        return null;
    }
}

public sealed class PaintRuleEngine
{
    private readonly RuleConfig config;
    private readonly IStdCorrosionLogic corrosionLogic;

    public PaintRuleEngine(RuleConfig config, IStdCorrosionLogic? corrosionLogic = null)
    {
        this.config = config;
        this.corrosionLogic = corrosionLogic ?? new ReviewOnSpecialConditions();
    }

    public EvaluationResult Evaluate(PipeRunData run)
    {
        MaterialCategory material = MaterialCategory.UNKNOWN;
        string group = "", specStatus = "";
        InsulationCondition insulation = InsulationCondition.Review;
        var codes = Array.Empty<string>();
        string? resultCode = null;
        EvaluationResult Result(PaintStatus status, string reason) =>
            new(run, material, group, specStatus, insulation, codes, resultCode, status, reason);

        if (run.IsUnderground == true) return Result(PaintStatus.NOT_APPLICABLE, "Underground piping");
        if (run.IsUnderground is null && !config.Settings.IgnoreUnknownUnderground)
            return Result(PaintStatus.REVIEW, "Underground flag unknown");

        string spec = (run.Spec ?? "").Trim().ToUpperInvariant();
        if (spec.Length == 0) return Result(PaintStatus.REVIEW, "Spec missing");
        if (!config.SpecMaterials.TryGetValue(spec, out var mapped))
            return Result(PaintStatus.REVIEW, "Spec not mapped");
        material = mapped.Category; group = mapped.MaterialGroup; specStatus = mapped.Status.ToString();
        if (mapped.Status == SpecStatus.Obsolete)
            return Result(PaintStatus.REVIEW, "Obsolete piping class");

        insulation = ClassifyInsulation(run);
        if (insulation == InsulationCondition.PP)
            insulation = config.Settings.PersonnelProtectionAs;
        if (insulation == InsulationCondition.Review)
            return Result(PaintStatus.REVIEW, "Insulation condition unresolved");

        var special = corrosionLogic.ReviewReason(run, material, insulation);
        if (special is not null) return Result(PaintStatus.REVIEW, special);

        var temperature = run.MaxOperatingTemp ?? run.OperatingTemp;
        var applicable = config.PaintingRules.Where(x =>
            x.Project == config.Settings.Project && x.Category == material &&
            (x.InsulationCondition == "*" || x.InsulationCondition == insulation.ToString())).ToList();
        if (temperature is null && applicable.Any(x => x.TempMin is not null || x.TempMax is not null))
            return Result(PaintStatus.REVIEW, "Operating temperature missing");
        var rows = applicable.Where(x =>
            (x.TempMin is null || temperature >= x.TempMin) &&
            (x.TempMax is null || temperature <= x.TempMax)).ToList();
        if (rows.Count == 0)
            return Result(PaintStatus.REVIEW, "No rule for material/insulation/temperature");
        if (config.Settings.UseLowestApplicableBand)
        {
            var rank = rows.Min(x => x.BandRank);
            rows = rows.Where(x => x.BandRank == rank).ToList();
        }
        codes = rows.Select(x => x.PaintCode).Distinct(StringComparer.Ordinal).ToArray();
        if (codes.Length > 1 || rows.Any(x => !x.AutoAssignable))
            return Result(PaintStatus.REVIEW, "Multiple candidates: " + string.Join(", ", codes));

        resultCode = codes.Single();
        string hint = material == MaterialCategory.CS &&
            insulation is InsulationCondition.Insulated or InsulationCondition.H3 &&
            temperature > 177
            ? "STD-ENG-MS012 N1: >177°C continuous may allow No Paint - engineer to confirm"
            : "";
        if (run.ExistingPaintCode == resultCode) return Result(PaintStatus.OK, hint);
        if (!string.IsNullOrWhiteSpace(run.ExistingPaintCode) && !config.Settings.OverwriteExistingPaintCode)
            return Result(PaintStatus.MISMATCH, Join("Existing Paint Code differs", hint));
        if (config.Settings.DryRun) return Result(PaintStatus.WOULD_SET, hint);
        // AUTO_SET is a write intent. The S3D adapter must replace it with ERROR on failed commit.
        return Result(PaintStatus.AUTO_SET, hint);
    }

    private InsulationCondition ClassifyInsulation(PipeRunData run)
    {
        if (run.InsulationThickness == 0) return InsulationCondition.Exposed;
        string key = (run.InsulationPurpose ?? "").Trim();
        return config.InsulationMapping.TryGetValue(key, out var condition)
            ? condition : InsulationCondition.Review;
    }

    private static string Join(string first, string second) =>
        second.Length == 0 ? first : first + "; " + second;
}
