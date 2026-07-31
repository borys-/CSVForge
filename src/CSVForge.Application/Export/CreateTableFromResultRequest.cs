namespace CSVForge.Application.Export;

public sealed record CreateTableFromResultRequest(
    string SourceTableName,
    string TargetTableName,
    IReadOnlyList<string> Columns,
    string? SourceSql = null,
    IReadOnlyDictionary<string, IReadOnlyList<string?>>? ColumnFilters = null);
