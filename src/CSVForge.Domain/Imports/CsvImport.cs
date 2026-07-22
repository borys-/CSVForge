namespace CSVForge.Domain.Imports;

public sealed record CsvImport(
    Guid Id,
    string DisplayName,
    string SourcePath,
    string TableName,
    DateTimeOffset ImportedAt,
    long RowCount,
    IReadOnlyList<CsvColumn> Columns);
