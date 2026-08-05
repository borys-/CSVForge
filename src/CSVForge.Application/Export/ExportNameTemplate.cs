using System.Text;

namespace CSVForge.Application.Export;

public static class ExportNameTemplate
{
    public const string Default = "{liczba_rekordów}_{data}_{godzina}_{minuta}";

    public static string Format(string? template, long recordCount, DateTimeOffset timestamp)
    {
        string value = string.IsNullOrWhiteSpace(template) ? Default : template.Trim();
        value = value
            .Replace("{liczba_rekordów}", recordCount.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{data}", timestamp.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{godzina}", timestamp.ToString("HH"), StringComparison.OrdinalIgnoreCase)
            .Replace("{minuta}", timestamp.ToString("mm"), StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(value) ? Format(Default, recordCount, timestamp) : value;
    }

    public static string ForFile(string? template, long recordCount, DateTimeOffset timestamp)
    {
        string value = Format(template, recordCount, timestamp);
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
    }

    public static string ForTable(string? template, long recordCount, DateTimeOffset timestamp)
    {
        string value = Format(template, recordCount, timestamp);
        StringBuilder builder = new(value.Length + 1);
        foreach (char character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        string result = builder.ToString().Trim('_');
        if (result.Length == 0) result = "export";
        if (char.IsDigit(result[0])) result = "_" + result;
        return result.Length <= 64 ? result : result[..64];
    }
}
