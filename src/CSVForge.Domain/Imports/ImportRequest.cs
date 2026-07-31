namespace CSVForge.Domain.Imports;

public sealed record ImportRequest(
    string FilePath,
    string DisplayName,
    bool HasHeader,
    char? Delimiter,
    string? EncodingName,
    int BatchSize = 5000,
    bool AutoDetectHeader = false,
    IReadOnlyList<CsvColumnMapping>? ColumnMappings = null,
    string? SourcePath = null,
    string? StagingDatabasePath = null,
    string? StagingTableName = null);
