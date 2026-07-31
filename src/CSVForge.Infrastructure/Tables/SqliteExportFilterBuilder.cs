using CSVForge.Infrastructure.Csv;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Tables;

internal static class SqliteExportFilterBuilder
{
    public static string Build(SqliteCommand command, IReadOnlyList<string> availableColumns, IReadOnlyDictionary<string, IReadOnlyList<string?>>? columnFilters)
    {
        List<string> predicates = [];
        int filterIndex = 0;
        foreach ((string column, IReadOnlyList<string?> values) in columnFilters ?? new Dictionary<string, IReadOnlyList<string?>>())
        {
            if (!availableColumns.Contains(column, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException($"Filter column '{column}' does not exist.", nameof(columnFilters));
            if (values.Count == 0) { predicates.Add("0 = 1"); filterIndex++; continue; }
            List<string> valuePredicates = [];
            int valueIndex = 0;
            foreach (string? value in values)
            {
                if (value is null) valuePredicates.Add($"{CsvImportNameHelper.QuoteIdentifier(column)} IS NULL");
                else
                {
                    string parameter = $"$exportColumnFilter{filterIndex}_{valueIndex++}";
                    valuePredicates.Add($"{CsvImportNameHelper.QuoteIdentifier(column)} = {parameter}");
                    command.Parameters.AddWithValue(parameter, value);
                }
            }
            predicates.Add("(" + string.Join(" OR ", valuePredicates) + ")");
            filterIndex++;
        }
        return predicates.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", predicates);
    }
}
