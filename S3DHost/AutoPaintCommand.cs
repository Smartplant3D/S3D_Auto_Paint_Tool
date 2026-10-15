using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Ingr.SP3D.Common.Client;
using Ingr.SP3D.Common.Client.Services;
using Ingr.SP3D.Common.Middle;
using Ingr.SP3D.Common.Middle.Services;
using PaintRules;
using Reporting;
using S3DAdapter;

namespace YYB
{
    [Guid("89D91709-0E04-4AC2-AF02-8457722BC9EC")]
    [ProgId("YYB.AutoPaintCommand")]
    [ComVisible(true)]
    public sealed class AutoPaintCommand : BaseModalCommand
    {
        public override void OnStart(int instanceId, object argument)
        {
            try
            {
                var site = MiddleServiceProvider.SiteMgr.ActiveSite;
                if (site == null || site.ActivePlant == null ||
                    ClientServiceProvider.WorkingSet.ActiveConnection == null)
                    throw new InvalidOperationException("Please open an S3D Plant and model before running Auto Paint.");

                var assemblyDir = Path.GetDirectoryName(typeof(AutoPaintCommand).Assembly.Location);
                if (string.IsNullOrWhiteSpace(assemblyDir))
                    throw new InvalidOperationException("Cannot determine Auto Paint assembly directory.");
                var configDir = Path.Combine(assemblyDir, "config");
                if (!Directory.Exists(configDir))
                    throw new DirectoryNotFoundException("Auto Paint config directory is missing: " + configDir +
                        "\nCopy the config folder beside S3D_Auto_Paint_Tool.dll.");
                var config = ConfigLoader.Load(configDir);
                var reportPath = ChooseReportPath();
                if (reportPath == null) return;
                var scanner = new PipeRunScanner();
                var objects = scanner.ReadWorkingSet();
                var engine = new PaintRuleEngine(config);
                var results = objects.Select(x => engine.Evaluate(x.Data)).ToList();

                var planned = results.Where(x => x.Status == PaintStatus.AUTO_SET).ToList();
                if (!config.Settings.DryRun && planned.Count > 0)
                {
                    var previewPath = Path.Combine(Path.GetDirectoryName(reportPath)!,
                        Path.GetFileNameWithoutExtension(reportPath) + "-preview-" +
                        DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".xlsx");
                    ExcelReportExporter.Export(previewPath, results.Select(x => x.Status == PaintStatus.AUTO_SET
                        ? x with { Status = PaintStatus.WOULD_SET } : x).ToList(),
                        config.Settings, DateTimeOffset.Now, Environment.UserName);
                    var answer = MessageBox.Show(
                        "Scanned " + results.Count + " Working Set PipeRuns. " + planned.Count +
                        " have one approved Paint Code and may be modified.\n" +
                        "REVIEW/MISMATCH/NOT_APPLICABLE items will be skipped.\n" +
                        "Preview report: " + previewPath + "\n\n" +
                        "Write IJCoatingInfo.CoatingType for these " + planned.Count + " PipeRuns?",
                        "S3D Auto Paint", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (answer == DialogResult.Yes)
                    {
                        try { scanner.WriteBatch(objects, planned); }
                        catch (Exception ex)
                        {
                            var ids = new HashSet<string>(planned.Select(x => x.Run.Oid), StringComparer.Ordinal);
                            results = results.Select(x => ids.Contains(x.Run.Oid)
                                ? x with { Status = PaintStatus.ERROR, Reason = "Write transaction failed: " + ex.Message }
                                : x).ToList();
                        }
                    }
                    else
                    {
                        var ids = new HashSet<string>(planned.Select(x => x.Run.Oid), StringComparer.Ordinal);
                        results = results.Select(x => ids.Contains(x.Run.Oid)
                            ? x with { Status = PaintStatus.WOULD_SET, Reason = "Write cancelled by user" }
                            : x).ToList();
                    }
                }

                ExcelReportExporter.Export(reportPath, results, config.Settings,
                    DateTimeOffset.Now, Environment.UserName);
                var counts = string.Join("; ", results.GroupBy(x => x.Status)
                    .OrderBy(x => x.Key).Select(x => x.Key + ": " + x.Count()));
                MessageBox.Show("Scanned " + results.Count + " Working Set PipeRuns.\n" + counts +
                    "\n\nReport: " + reportPath, "S3D Auto Paint",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "S3D Auto Paint Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string? ChooseReportPath()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Save S3D Auto Paint Report";
                dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                dialog.DefaultExt = "xlsx";
                dialog.AddExtension = true;
                dialog.OverwritePrompt = true;
                dialog.CheckPathExists = true;
                dialog.RestoreDirectory = true;
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                dialog.FileName = "AutoPaint-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".xlsx";
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
            }
        }
    }

}
