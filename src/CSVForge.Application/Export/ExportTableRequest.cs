namespace CSVForge.Application.Export;

public sealed record ExportTableRequest(
    string TableName,
    string OutputPath,
    char Delimiter,
    bool IncludeHeader,
    string? TextFilter = null);
