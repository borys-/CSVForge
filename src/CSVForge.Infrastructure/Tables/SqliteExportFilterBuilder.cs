using CSVForge.Infrastructure.Csv;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Tables;

internal static class SqliteExportFilterBuilder
{
    public static string Build(
        SqliteCommand command,
        string? textFilter,
        IReadOnlyList<string> availableColumns,
        IReadOnlyDictionary<string, IReadOnlyList<string?>>? columnFilters)
    {
        HashSet<string> available = availableColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<string> predicates = [];
        if (!string.IsNullOrWhiteSpace(textFilter))
        {
            predicates.Add("(" + string.Join(" OR ", availableColumns.Select(column =>
                $"{CsvImportNameHelper.QuoteIdentifier(column)} LIKE $filter")) + ")");
            command.Parameters.AddWithValue("$filter", $"%{textFilter}%");
        }

        int filterIndex = 0;
        foreach ((string column, IReadOnlyList<string?> values) in columnFilters ?? new Dictionary<string, IReadOnlyList<string?>>())
        {
            if (!available.Contains(column))
            {
                throw new ArgumentException($"Kolumna filtra '{column}' nie istnieje.");
            }
            if (values.Count == 0)
            {
                predicates.Add("0 = 1");
                filterIndex++;
                continue;
            }

            List<string> parts = [];
            int valueIndex = 0;
            foreach (string? value in values)
            {
                if (value is null)
                {
                    parts.Add($"{CsvImportNameHelper.QuoteIdentifier(column)} IS NULL");
                }
                else
                {
                    string parameter = $"$exportColumnFilter{filterIndex}_{valueIndex++}";
                    parts.Add($"{CsvImportNameHelper.QuoteIdentifier(column)} = {parameter}");
                    command.Parameters.AddWithValue(parameter, value);
                }
            }
            predicates.Add("(" + string.Join(" OR ", parts) + ")");
            filterIndex++;
        }
        return predicates.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", predicates);
    }
}
