using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace AgentRPA.Application.Batch;

/// <summary>批量业务表格解析契约。输出统一为 JSON 可序列化的行数据。</summary>
public interface ISpreadsheetImportService
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> ReadAsync(Stream stream, string fileName, CancellationToken cancellationToken);
}

/// <summary>轻量 CSV/XLSX 解析器，不把 Office 组件安装到 NodeAgent。</summary>
public sealed class SpreadsheetImportService : ISpreadsheetImportService
{
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> ReadAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => await ReadCsvAsync(stream, cancellationToken),
            ".xlsx" => await ReadXlsxAsync(stream, cancellationToken),
            _ => throw new InvalidOperationException("仅支持 CSV 和 XLSX 文件。")
        };
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> ReadCsvAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<string[]>();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            rows.Add(ParseCsvLine(line).ToArray());
        }
        return ToRows(rows);
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> ReadXlsxAsync(Stream stream, CancellationToken ct)
    {
        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        memory.Position = 0;
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        var sharedStrings = ReadSharedStrings(archive);
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var relationship = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        var firstSheet = workbook.Descendants().FirstOrDefault(x => x.Name.LocalName == "sheet")
            ?? throw new InvalidOperationException("XLSX 不包含工作表。");
        var relationshipId = firstSheet.Attributes().FirstOrDefault(x => x.Name.LocalName == "id")?.Value;
        var target = relationship.Descendants().FirstOrDefault(x => x.Attributes().Any(a => a.Name.LocalName == "Id" && a.Value == relationshipId))?.Attribute("Target")?.Value;
        if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("无法解析 XLSX 第一张工作表。");
        var sheetPath = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
        if (sheetPath.Contains("../", StringComparison.Ordinal)) sheetPath = "xl/worksheets/sheet1.xml";
        var sheet = LoadXml(archive, sheetPath);
        var rows = new List<string[]>();
        foreach (var row in sheet.Descendants().Where(x => x.Name.LocalName == "row"))
        {
            ct.ThrowIfCancellationRequested();
            var values = new Dictionary<int, string?>();
            foreach (var cell in row.Elements().Where(x => x.Name.LocalName == "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? string.Empty;
                var column = ColumnIndex(reference);
                var type = cell.Attribute("t")?.Value;
                var value = cell.Elements().FirstOrDefault(x => x.Name.LocalName == "v")?.Value ?? string.Empty;
                if (type == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex < sharedStrings.Count) value = sharedStrings[sharedIndex];
                else if (type == "inlineStr") value = cell.Descendants().FirstOrDefault(x => x.Name.LocalName == "t")?.Value ?? string.Empty;
                values[column] = value;
            }
            var max = values.Keys.DefaultIfEmpty(-1).Max();
            var array = Enumerable.Range(0, max + 1).Select(i => values.TryGetValue(i, out var value) ? value ?? string.Empty : string.Empty).ToArray();
            rows.Add(array);
        }
        return ToRows(rows);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        using var stream = entry.Open();
        var xml = XDocument.Load(stream);
        return xml.Descendants().Where(x => x.Name.LocalName == "si").Select(x => string.Concat(x.Descendants().Where(t => t.Name.LocalName == "t").Select(t => t.Value))).ToArray();
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidOperationException($"XLSX 文件缺少 {path}。 ");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static int ColumnIndex(string reference)
    {
        var index = 0;
        foreach (var ch in reference.TakeWhile(char.IsLetter)) index = index * 26 + char.ToUpperInvariant(ch) - 'A' + 1;
        return Math.Max(0, index - 1);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string?>> ToRows(IReadOnlyList<string[]> rows)
    {
        if (rows.Count == 0) return [];
        var headers = rows[0].Select((value, index) => string.IsNullOrWhiteSpace(value) ? $"Column{index + 1}" : value.Trim()).ToArray();
        return rows.Skip(1).Where(row => row.Any(x => !string.IsNullOrWhiteSpace(x))).Select(row =>
            (IReadOnlyDictionary<string, string?>)headers.Select((header, index) => new KeyValuePair<string, string?>(header, index < row.Length ? row[index] : null)).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    private static IEnumerable<string> ParseCsvLine(string line)
    {
        var value = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted) { yield return value.ToString(); value.Clear(); }
            else value.Append(ch);
        }
        yield return value.ToString();
    }
}
