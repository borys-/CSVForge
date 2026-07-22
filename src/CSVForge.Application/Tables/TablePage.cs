namespace CSVForge.Application.Tables;

public sealed record TablePage(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    long TotalRows,
    int Limit,
    int Offset);
