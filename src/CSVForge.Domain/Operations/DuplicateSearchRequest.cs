namespace CSVForge.Domain.Operations;

public sealed record DuplicateSearchRequest(
    string TableName,
    IReadOnlyList<string> KeyColumns,
    DuplicateSearchMode Mode,
    bool IgnoreEmptyValues);
