namespace CSVForge.Application.Operations;

public sealed record WorkspaceOperation(
    Guid Id,
    string OperationType,
    string? ResultTableName,
    DateTimeOffset CreatedAt,
    string Message,
    string? SourceSql = null)
{
    public string DisplayName => $"{CreatedAt:HH:mm} {OperationType}: {Message}";
}
