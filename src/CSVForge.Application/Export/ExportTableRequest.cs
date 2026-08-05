namespace CSVForge.Application.Export;

public sealed record ExportTableRequest(
    string TableName,
    string OutputPath,
    char Delimiter,
    bool IncludeHeader,
    IReadOnlyList<string>? Columns = null,
    string? SourceSql = null,
    IReadOnlyDictionary<string, IReadOnlyList<string?>>? ColumnFilters = null,
    bool ProtectExcelFormulas = true);
