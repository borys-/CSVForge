using System.Text;

namespace CSVForge.Infrastructure.Csv;

internal static class CsvImportNameHelper
{
    private static readonly char[] CandidateDelimiters = [';', ',', '\t'];

    public static async Task<char> DetectDelimiterAsync(string filePath, Encoding encoding, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(filePath);
        using StreamReader reader = new(stream, encoding, detectEncodingFromByteOrderMarks: true);

        string sample = string.Empty;
        for (int i = 0; i < 5; i++)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            sample += line;
        }

        return CandidateDelimiters
            .Select(delimiter => new { Delimiter = delimiter, Count = sample.Count(character => character == delimiter) })
            .OrderByDescending(candidate => candidate.Count)
            .FirstOrDefault(candidate => candidate.Count > 0)
            ?.Delimiter ?? ',';
    }

    public static IReadOnlyList<string> NormalizeColumns(IReadOnlyList<string> headers)
    {
        Dictionary<string, int> seenNames = new(StringComparer.OrdinalIgnoreCase);
        List<string> columns = [];

        for (int i = 0; i < headers.Count; i++)
        {
            string originalName = string.IsNullOrWhiteSpace(headers[i]) ? $"Column{i + 1}" : headers[i];
            columns.Add(MakeUnique(NormalizeIdentifier(originalName, "Column"), seenNames));
        }

        return columns;
    }

    public static IReadOnlyList<string> GenerateColumns(int count)
    {
        return Enumerable.Range(1, count).Select(index => $"Column{index}").ToArray();
    }

    public static string CreateTableName(string displayName)
    {
        return $"import_{NormalizeIdentifier(displayName, "csv")}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
    }

    public static string QuoteIdentifier(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string NormalizeIdentifier(string value, string fallback)
    {
        StringBuilder builder = new();

        foreach (char character in value.Trim())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        string normalized = builder.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = fallback;
        }

        if (char.IsDigit(normalized[0]))
        {
            normalized = $"{fallback}_{normalized}";
        }

        return normalized;
    }

    private static string MakeUnique(string name, IDictionary<string, int> seenNames)
    {
        if (!seenNames.TryGetValue(name, out int count))
        {
            seenNames[name] = 1;
            return name;
        }

        count++;
        seenNames[name] = count;
        return $"{name}_{count}";
    }
}
