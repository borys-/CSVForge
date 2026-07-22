namespace CSVForge.Domain.Imports;

public sealed record ImportResult(
    CsvImport Import,
    IReadOnlyList<ImportError> Errors);
