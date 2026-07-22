namespace CSVForge.Domain.Imports;

public sealed record ImportError(
    long RowNumber,
    string Message,
    string? RawRow);
