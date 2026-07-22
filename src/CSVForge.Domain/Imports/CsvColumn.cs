namespace CSVForge.Domain.Imports;

public sealed record CsvColumn(
    string OriginalName,
    string Name,
    int Index);
