using System.Globalization;

namespace CSVForge.Infrastructure.Csv;

internal static class CsvHeaderDetector
{
    private static readonly HashSet<string> CommonHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "name", "nazwa", "email", "e_mail", "city", "miasto", "date", "data",
        "address", "adres", "phone", "telefon", "code", "kod", "description", "opis"
    };

    public static bool LooksLikeHeader(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        if (first.Count == 0 || first.Count != second.Count || first.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        IReadOnlyList<string> normalized = CsvImportNameHelper.NormalizeColumns(first);
        if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != first.Count)
        {
            return false;
        }

        if (normalized.Any(CommonHeaders.Contains))
        {
            return true;
        }

        return first.Zip(second).Any(pair => !LooksLikeValue(pair.First) && LooksLikeValue(pair.Second));
    }

    public static bool LooksLikeReportPreamble(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        return first.Count == 1
            && second.Count > 1
            && first[0].Contains(':', StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(first[0]);
    }

    public static int GetSharedTrailingEmptyColumnCount(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        int maximum = Math.Min(first.Count, second.Count) - 1;
        int count = 0;
        while (count < maximum
               && string.IsNullOrWhiteSpace(first[first.Count - count - 1])
               && string.IsNullOrWhiteSpace(second[second.Count - count - 1]))
        {
            count++;
        }

        return count;
    }

    public static string[] TrimTrailingEmptyColumns(string[] record, int count)
    {
        if (count <= 0 || record.Length <= count
            || record.Skip(record.Length - count).Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            return record;
        }

        return record[..^count];
    }

    private static bool LooksLikeValue(string value)
    {
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
            || DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            || bool.TryParse(value, out _)
            || value.Contains('@');
    }
}
