using PaintRules;
using S3DAdapter;

var config = ConfigLoader.Load(Path.Combine(AppContext.BaseDirectory, "config"));
int passed = 0;
void Check(int id, PipeRunData run, PaintStatus expected, string[]? codes = null,
    bool lowest = true, InsulationCondition ppAs = InsulationCondition.Review,
    bool dryRun = true, string? reasonContains = null)
{
    var settings = config.Settings with { UseLowestApplicableBand = lowest,
        PersonnelProtectionAs = ppAs, DryRun = dryRun };
    var result = new PaintRuleEngine(config with { Settings = settings }).Evaluate(run);
    if (result.Status != expected ||
        (codes is not null && !result.CandidatePaintCodes.SequenceEqual(codes)) ||
        (reasonContains is not null && result.Reason.IndexOf(reasonContains, StringComparison.Ordinal) < 0))
        throw new Exception($"Case {id}: expected {expected} [{string.Join(",", codes ?? [])}], got {result.Status} [{string.Join(",", result.CandidatePaintCodes)}] reason={result.Reason}");
    passed++;
}

PipeRunData Run(string? spec, decimal? temp, string purpose = "None", decimal? thickness = 0,
    string? existing = null, bool? underground = false,
    IReadOnlyDictionary<string, bool?>? flags = null) =>
    new("oid", "run", "pipeline", spec, temp, null, null, purpose, thickness, null,
        underground, existing, flags);

Check(1, Run("1CBB01", 60), PaintStatus.WOULD_SET, ["C093U3"]);
Check(2, Run("1CBB01", 60), PaintStatus.REVIEW,
    ["C093U3", "C230U", "TSA230U", "C400U", "TSA400U"], lowest: false);
Check(3, Run("1CBB01", 60, "Heat Conservation", 25), PaintStatus.WOULD_SET, ["C093N"]);
Check(4, Run("1CBB01", 60, "H3", 25), PaintStatus.WOULD_SET, ["C093U3"]);
Check(5, Run("1CBB01", 93), PaintStatus.WOULD_SET, ["C093U3"]);
Check(6, Run("1CBB01", 150), PaintStatus.REVIEW, ["C230U", "TSA230U"]);
Check(7, Run("1CBB01", 150, "Heat Conservation", 25), PaintStatus.REVIEW, ["C230N", "TSA230N"]);
Check(8, Run("1CBB01", 150, "H3", 25), PaintStatus.WOULD_SET, ["C230U"]);
Check(9, Run("1CBB01", 300, "Heat Conservation", 25), PaintStatus.WOULD_SET,
    ["TSA400N"], reasonContains: "N1");
Check(10, Run("1CBB01", 300), PaintStatus.REVIEW, ["C400U", "TSA400U"]);
Check(11, Run("1CBB01", 300, "H3", 25), PaintStatus.WOULD_SET, ["C400U"]);
Check(12, Run("1CBB01", 450), PaintStatus.REVIEW, []);
Check(13, Run("1CBB01", -50, "Heat Conservation", 25), PaintStatus.REVIEW, []);
Check(14, Run("1CBB01", null), PaintStatus.REVIEW, reasonContains: "temperature missing");
Check(15, Run("1ALA01", 20, "Heat Conservation", 25), PaintStatus.WOULD_SET, ["NOPT"]);
Check(16, Run("1ALA01", null), PaintStatus.REVIEW, reasonContains: "temperature missing");
Check(17, Run("WSC020", 20), PaintStatus.REVIEW, reasonContains: "Obsolete");
Check(18, Run("1XYZ99", 20), PaintStatus.REVIEW, reasonContains: "not mapped");
Check(19, Run(" 1cbb01 ", 60, "Heat Conservation", 25), PaintStatus.WOULD_SET, ["C093N"]);
Check(20, Run("1CBB01", 60, "Personnel Protection", 25), PaintStatus.REVIEW);
Check(21, Run("1CBB01", 60, "Personnel Protection", 25), PaintStatus.WOULD_SET,
    ["C093U3"], ppAs: InsulationCondition.Exposed);
Check(22, Run("1CBB01", 60, underground: true), PaintStatus.NOT_APPLICABLE);
Check(23, Run("1CBB01", 60, "Heat Conservation", 25,
    flags: new Dictionary<string, bool?> { ["C5"] = true }), PaintStatus.REVIEW,
    reasonContains: "C5");
Check(24, Run("1CBB01", 60, "Heat Conservation", 25, "C093N"), PaintStatus.OK, ["C093N"]);
Check(25, Run("1CBB01", 60, "Heat Conservation", 25, "C230N"), PaintStatus.MISMATCH, ["C093N"]);
Check(26, Run("1CBB01", 60, "Heat Conservation", 25), PaintStatus.AUTO_SET,
    ["C093N"], dryRun: false);

var duplicate = Path.Combine(Path.GetTempPath(), "paint-rules-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(duplicate);
try
{
    foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "config")))
        File.Copy(file, Path.Combine(duplicate, Path.GetFileName(file)));
    var specs = Path.Combine(duplicate, "SpecMaterialMapping.csv");
    File.AppendAllText(specs, "1CBB01,CS,Carbon Steel,Active,Test,,\n");
    try { ConfigLoader.Load(duplicate); throw new Exception("Case 27 did not reject duplicate Spec"); }
    catch (InvalidDataException ex) when (ex.Message.Contains("Duplicate Spec")) { passed++; }
}
finally { Directory.Delete(duplicate, true); }

// Additional guards around source precedence, unknown flags and exact mapping.
Check(28, Run("1CBB01", 60, "Heat Conservation", 25, flags:
    new Dictionary<string, bool?> { ["N5"] = null }), PaintStatus.REVIEW, reasonContains: "N5");
Check(29, Run("1CBB01", 60, underground: null), PaintStatus.REVIEW, reasonContains: "Underground");
Check(30, Run("1CBB01-extra", 60), PaintStatus.REVIEW, reasonContains: "not mapped");
if (TemperatureUnits.KelvinToCelsius(273.15m) != 0m ||
    TemperatureUnits.KelvinToCelsius(373.15m) != 100m ||
    TemperatureUnits.KelvinToCelsius(233.15m) != -40m ||
    TemperatureUnits.KelvinToCelsius(450.15m) != 177m ||
    TemperatureUnits.KelvinToCelsius((decimal?)null) is not null)
    throw new Exception("Kelvin to Celsius conversion failed");
passed++;
Console.WriteLine($"Passed {passed} cases.");
