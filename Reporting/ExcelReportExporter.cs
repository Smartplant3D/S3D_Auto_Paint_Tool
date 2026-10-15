using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using PaintRules;

namespace Reporting;

public static class ExcelReportExporter
{
    private static readonly string[] DetailHeaders =
    [
        "PipeRun", "Pipeline", "Spec", "MaterialCategory", "MaterialGroup", "SpecStatus",
        "MaxOperatingTemp_C", "OperatingTemp_C", "DesignTemp_C", "InsulationPurpose",
        "InsulationCondition", "ExistingPaintCode", "CandidatePaintCodes", "ResultPaintCode",
        "Status", "Reason", "MappingVersion", "PaintRuleVersion", "RunTime", "User"
    ];

    public static Task ExportAsync(string path, IReadOnlyList<EvaluationResult> results,
        AppSettings settings, DateTimeOffset runTime, string user, CancellationToken token = default) =>
        Task.Run(() => Export(path, results, settings, runTime, user, token), token);

    public static void Export(string path, IReadOnlyList<EvaluationResult> results,
        AppSettings settings, DateTimeOffset runTime, string user, CancellationToken token = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        Write(zip, "[Content_Types].xml", w =>
        {
            w.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
            w.WriteStartElement("Default"); w.WriteAttributeString("Extension", "rels"); w.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml"); w.WriteEndElement();
            w.WriteStartElement("Default"); w.WriteAttributeString("Extension", "xml"); w.WriteAttributeString("ContentType", "application/xml"); w.WriteEndElement();
            w.WriteStartElement("Override"); w.WriteAttributeString("PartName", "/xl/workbook.xml"); w.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"); w.WriteEndElement();
            for (int i = 1; i <= 3; i++)
            {
                w.WriteStartElement("Override"); w.WriteAttributeString("PartName", $"/xl/worksheets/sheet{i}.xml");
                w.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"); w.WriteEndElement();
            }
            w.WriteEndElement();
        });
        Write(zip, "_rels/.rels", w =>
        {
            w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            w.WriteStartElement("Relationship"); w.WriteAttributeString("Id", "rId1");
            w.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
            w.WriteAttributeString("Target", "xl/workbook.xml"); w.WriteEndElement(); w.WriteEndElement();
        });
        Write(zip, "xl/workbook.xml", w =>
        {
            w.WriteStartElement("workbook", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            w.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            w.WriteStartElement("sheets");
            var names = new[] { "Details", "Summary", "UnmappedSpecs" };
            for (int i = 0; i < names.Length; i++)
            {
                w.WriteStartElement("sheet"); w.WriteAttributeString("name", names[i]); w.WriteAttributeString("sheetId", (i + 1).ToString());
                w.WriteAttributeString("r", "id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships", $"rId{i + 1}"); w.WriteEndElement();
            }
            w.WriteEndElement(); w.WriteEndElement();
        });
        Write(zip, "xl/_rels/workbook.xml.rels", w =>
        {
            w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            for (int i = 1; i <= 3; i++)
            {
                w.WriteStartElement("Relationship"); w.WriteAttributeString("Id", $"rId{i}");
                w.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
                w.WriteAttributeString("Target", $"worksheets/sheet{i}.xml"); w.WriteEndElement();
            }
            w.WriteEndElement();
        });
        var details = new List<IReadOnlyList<object?>>() { DetailHeaders };
        foreach (var r in results)
        {
            token.ThrowIfCancellationRequested();
            details.Add(new object?[] { r.Run.Name, r.Run.PipelineName, r.Run.Spec, r.MaterialCategory.ToString(),
                r.MaterialGroup, r.SpecStatus, r.Run.MaxOperatingTemp, r.Run.OperatingTemp, r.Run.DesignTemp,
                r.Run.InsulationPurpose, r.InsulationCondition.ToString(), r.Run.ExistingPaintCode,
                string.Join(", ", r.CandidatePaintCodes), r.ResultPaintCode, r.Status.ToString(), r.Reason,
                settings.MappingVersion, settings.PaintRuleVersion, runTime.ToString("O"), user });
        }
        Sheet(zip, 1, details);
        var summary = new List<IReadOnlyList<object?>>() { new object?[] { "Status", "Count" } };
        foreach (PaintStatus status in Enum.GetValues(typeof(PaintStatus)))
            summary.Add(new object?[] { status.ToString(), results.Count(x => x.Status == status) });
        Sheet(zip, 2, summary);
        var unmapped = new List<IReadOnlyList<object?>>() { new object?[] { "Spec", "Count" } };
        foreach (var group in results.Where(x => x.Reason == "Spec not mapped")
            .GroupBy(x => (x.Run.Spec ?? "").Trim().ToUpperInvariant()).OrderBy(x => x.Key))
            unmapped.Add(new object?[] { group.Key, group.Count() });
        Sheet(zip, 3, unmapped);
    }

    private static void Sheet(ZipArchive zip, int id, IEnumerable<IReadOnlyList<object?>> rows) =>
        Write(zip, $"xl/worksheets/sheet{id}.xml", w =>
        {
            w.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            w.WriteStartElement("sheetData"); int rowNum = 0;
            foreach (var row in rows)
            {
                w.WriteStartElement("row"); w.WriteAttributeString("r", (++rowNum).ToString());
                for (int col = 0; col < row.Count; col++)
                {
                    var value = row[col]; if (value is null) continue;
                    w.WriteStartElement("c"); w.WriteAttributeString("r", Column(col) + rowNum);
                    if (value is decimal or int)
                    {
                        w.WriteElementString("v", Convert.ToString(value, CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        w.WriteAttributeString("t", "inlineStr"); w.WriteStartElement("is");
                        w.WriteElementString("t", value.ToString()); w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }
                w.WriteEndElement();
            }
            w.WriteEndElement(); w.WriteEndElement();
        });

    private static string Column(int zeroBased)
    {
        var value = zeroBased + 1; var answer = "";
        while (value > 0) { value--; answer = (char)('A' + value % 26) + answer; value /= 26; }
        return answer;
    }

    private static void Write(ZipArchive zip, string name, Action<XmlWriter> action)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false });
        writer.WriteStartDocument(); action(writer); writer.WriteEndDocument();
    }
}
