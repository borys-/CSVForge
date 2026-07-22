namespace CSVForge.Application.Tables;

public sealed record BrowseTableRequest(
    string TableName,
    int Limit,
    int Offset,
    string? SortColumn,
    bool SortDescending,
    string? TextFilter);
