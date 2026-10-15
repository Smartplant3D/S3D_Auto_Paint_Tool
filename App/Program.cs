using System.Globalization;
using PaintRules;
using Reporting;

if (args.Length > 0 && args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: App [input CSV] [output XLSX]");
    Console.WriteLine("Default input: samples/piperuns.csv; output: report.xlsx");
    return;
}

string root = AppContext.BaseDirectory;
string input = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(root, "samples", "piperuns.csv");
string output = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(Environment.CurrentDirectory, "report.xlsx");
var config = ConfigLoader.Load(Path.Combine(root, "config"));
if (!config.Settings.DryRun)
    throw new InvalidOperationException("CSV demo has no S3D writer; set DryRun=true in config/appsettings.json.");
var engine = new PaintRuleEngine(config);
var data = Csv.Read(input).Select(x => new PipeRunData(
    x["Oid"], x["Name"], x["PipelineName"], x["Spec"],
    ConfigLoader.Number(x["MaxOperatingTemp_C"]), ConfigLoader.Number(x["OperatingTemp_C"]),
    ConfigLoader.Number(x["DesignTemp_C"]), x["InsulationPurpose"],
    ConfigLoader.Number(x["InsulationThickness"]), x["InsulationSpec"],
    x["ExistingPaintCode"])).ToList();
var results = data.Select(engine.Evaluate).ToList();
var now = DateTimeOffset.Now;
await ExcelReportExporter.ExportAsync(output, results, config.Settings, now, Environment.UserName);
Console.WriteLine($"Report: {output}");
foreach (var group in results.GroupBy(x => x.Status).OrderBy(x => x.Key))
    Console.WriteLine($"{group.Key}: {group.Count()}");
Console.WriteLine($"MappingVersion={config.Settings.MappingVersion}; PaintRuleVersion={config.Settings.PaintRuleVersion}; User={Environment.UserName}; Time={now:O}; Scope={input}");
