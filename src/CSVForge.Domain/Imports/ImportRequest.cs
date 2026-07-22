namespace CSVForge.Domain.Imports;

public sealed record ImportRequest(
    string FilePath,
    string DisplayName,
    bool HasHeader,
    char? Delimiter,
    string? EncodingName,
    int BatchSize = 500,
    bool AutoDetectHeader = false,
    IReadOnlyList<CsvColumnMapping>? ColumnMappings = null);
