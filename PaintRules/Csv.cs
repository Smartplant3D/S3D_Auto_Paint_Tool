using System.Text;

namespace PaintRules;

public static class Csv
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Read(string path)
    {
        var lines = File.ReadAllText(path, Encoding.UTF8);
        var records = Parse(lines).Where(x => x.Count > 1 || x[0].Length > 0).ToList();
        if (records.Count == 0) throw new InvalidDataException($"Empty CSV: {path}");
        var headers = records[0];
        if (headers.Count != headers.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidDataException($"Duplicate CSV header: {path}");
        return records.Skip(1).Select((fields, index) =>
        {
            if (fields.Count != headers.Count)
                throw new InvalidDataException($"CSV column count at {path}:{index + 2}");
            return (IReadOnlyDictionary<string, string>)headers.Zip(fields, (header, value) => new { header, value })
                .ToDictionary(x => x.header, x => x.value, StringComparer.Ordinal);
        }).ToList();
    }

    private static List<List<string>> Parse(string input)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < input.Length && input[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"')
            {
                if (field.Length != 0) throw new InvalidDataException("Unexpected CSV quote");
                quoted = true;
            }
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < input.Length && input[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
                row = new List<string>();
            }
            else field.Append(c);
        }
        if (quoted) throw new InvalidDataException("Unterminated CSV quote");
        if (row.Count > 0 || field.Length > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
