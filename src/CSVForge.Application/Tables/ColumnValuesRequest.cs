namespace CSVForge.Application.Tables;

public sealed record ColumnValuesRequest(
    string TableName,
    string ColumnName,
    string? TextFilter,
    IReadOnlyDictionary<string, IReadOnlyList<string?>>? ColumnFilters = null,
    int Limit = 500);
