namespace CSVForge.Domain.Operations;

public sealed record DatasetCompareRequest(
    string LeftTableName,
    string RightTableName,
    IReadOnlyList<string> LeftKeyColumns,
    IReadOnlyList<string> RightKeyColumns,
    DatasetCompareMode Mode);
