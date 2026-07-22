namespace CSVForge.Domain.Imports;

public sealed record ImportProgress(
    long ProcessedRows,
    long? TotalRows,
    string CurrentStep);
