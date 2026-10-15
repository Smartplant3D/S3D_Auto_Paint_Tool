using System.Globalization;
using System.Web.Script.Serialization;

namespace PaintRules;

public static class ConfigLoader
{
    public static RuleConfig Load(string directory)
    {
        string PathIn(string name) => Path.Combine(directory, name);
        var settingsDto = new JavaScriptSerializer().Deserialize<SettingsDto>(File.ReadAllText(PathIn("appsettings.json")))
            ?? throw new InvalidDataException("Empty appsettings.json");
        var settings = new AppSettings(Required(settingsDto.Project, "Project"),
            Required(settingsDto.MappingVersion, "MappingVersion"),
            Required(settingsDto.PaintRuleVersion, "PaintRuleVersion"),
            settingsDto.UseLowestApplicableBand ?? throw new InvalidDataException("Missing UseLowestApplicableBand"),
            ParseEnum<InsulationCondition>(Required(settingsDto.PersonnelProtectionAs, "PersonnelProtectionAs")),
            settingsDto.DryRun ?? throw new InvalidDataException("Missing DryRun"),
            settingsDto.OverwriteExistingPaintCode ?? throw new InvalidDataException("Missing OverwriteExistingPaintCode"),
            settingsDto.IgnoreUnknownUnderground);
        if (settings.PersonnelProtectionAs is not (InsulationCondition.Exposed or InsulationCondition.Insulated or InsulationCondition.Review))
            throw new InvalidDataException("PersonnelProtectionAs must be Exposed, Insulated or Review");

        var specs = new Dictionary<string, SpecMaterial>(StringComparer.Ordinal);
        foreach (var row in Csv.Read(PathIn("SpecMaterialMapping.csv")))
        {
            var spec = Required(row["Spec"].Trim().ToUpperInvariant(), "Spec");
            if (specs.ContainsKey(spec))
                throw new InvalidDataException($"Duplicate Spec: {spec}");
            specs.Add(spec, new SpecMaterial(spec,
                ParseEnum<MaterialCategory>(row["MaterialCategory"]), row["MaterialGroup"],
                ParseEnum<SpecStatus>(row["Status"]), row["Source"], row["Revision"], row["Notes"]));
        }

        var rules = Csv.Read(PathIn("PaintingRules.csv")).Select(row =>
        {
            var condition = row["InsulationCondition"];
            if (condition != "*") ParseEnum<InsulationCondition>(condition);
            var min = Number(row["TempMin"]); var max = Number(row["TempMax"]);
            if (min > max) throw new InvalidDataException("TempMin exceeds TempMax");
            if (!int.TryParse(row["BandRank"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) || rank < 0)
                throw new InvalidDataException("Invalid BandRank");
            if (row["AutoAssignable"] is not ("Y" or "N"))
                throw new InvalidDataException("AutoAssignable must be Y or N");
            return new PaintingRule(Required(row["Project"], "Project"),
                ParseEnum<MaterialCategory>(row["MaterialCategory"]), condition, min, max, rank,
                Required(row["PaintCode"], "PaintCode"), row["AutoAssignable"] == "Y", row["Notes"]);
        }).ToList();
        if (!rules.Any(x => x.Project == settings.Project)) throw new InvalidDataException("No rules for configured Project");

        var insulation = new Dictionary<string, InsulationCondition>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Csv.Read(PathIn("InsulationMapping.csv")))
        {
            var key = row["S3DInsulationPurpose"].Trim();
            if (insulation.ContainsKey(key))
                throw new InvalidDataException($"Duplicate insulation purpose: {key}");
            insulation.Add(key, ParseEnum<InsulationCondition>(row["InsulationCondition"]));
        }
        return new RuleConfig(settings, specs, rules, insulation);
    }

    public static decimal? Number(string? text) => string.IsNullOrWhiteSpace(text) ? null :
        decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string Required(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value) ? value! : throw new InvalidDataException($"Missing {name}");

    private static T ParseEnum<T>(string text) where T : struct, Enum =>
        Enum.TryParse<T>(text, false, out var value) && Enum.IsDefined(typeof(T), value)
            ? value : throw new InvalidDataException($"Invalid {typeof(T).Name}: {text}");

    private sealed class SettingsDto
    {
        public string? Project { get; set; }
        public string? MappingVersion { get; set; }
        public string? PaintRuleVersion { get; set; }
        public bool? UseLowestApplicableBand { get; set; }
        public string? PersonnelProtectionAs { get; set; }
        public bool? DryRun { get; set; }
        public bool? OverwriteExistingPaintCode { get; set; }
        public bool IgnoreUnknownUnderground { get; set; }
    }
}
