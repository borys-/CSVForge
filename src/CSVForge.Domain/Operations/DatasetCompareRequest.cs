namespace CSVForge.Domain.Operations;

public sealed record DatasetCompareSource(
    string TableName,
    string DisplayName,
    IReadOnlyList<string> KeyColumns);

public sealed record DatasetCompareRequest(
    IReadOnlyList<DatasetCompareSource> Sources,
    DatasetCompareMode Mode)
{
    public DatasetCompareRequest(
        string leftTableName,
        string rightTableName,
        IReadOnlyList<string> leftKeyColumns,
        IReadOnlyList<string> rightKeyColumns,
        DatasetCompareMode mode)
        : this(
        [
            new DatasetCompareSource(leftTableName, "plik 1", leftKeyColumns),
            new DatasetCompareSource(rightTableName, "plik 2", rightKeyColumns)
        ], mode)
    {
    }
}
