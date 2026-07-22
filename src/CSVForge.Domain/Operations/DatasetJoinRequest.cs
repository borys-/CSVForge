namespace CSVForge.Domain.Operations;

public sealed record DatasetJoinRequest(
    string LeftTableName,
    string RightTableName,
    IReadOnlyList<string> LeftJoinColumns,
    IReadOnlyList<string> RightJoinColumns,
    IReadOnlyList<string> LeftOutputColumns,
    IReadOnlyList<string> RightOutputColumns,
    DatasetJoinType JoinType);
