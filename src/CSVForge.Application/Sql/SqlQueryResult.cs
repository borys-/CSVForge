namespace CSVForge.Application.Sql;

public sealed record SqlQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    int AffectedRows,
    bool WasTruncated);
