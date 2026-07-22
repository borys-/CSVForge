namespace CSVForge.Application.Export;

public sealed record ExportResult(
    string FilePath,
    long ExportedRows);
