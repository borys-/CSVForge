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

    private static bool LooksLikeValue(string value)
    {
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
            || DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            || bool.TryParse(value, out _)
            || value.Contains('@');
    }
}
