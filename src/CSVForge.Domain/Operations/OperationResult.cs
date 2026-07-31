namespace CSVForge.Domain.Operations;

public sealed record OperationResult(
    bool Success,
    string? ResultTableName,
    string Message,
    string? Sql = null)
{
    public static OperationResult Ok(string resultTableName, string message, string? sql = null)
    {
        return new OperationResult(true, resultTableName, message, sql);
    }

    public static OperationResult OkQuery(string sql, string message)
    {
        return new OperationResult(true, null, message, sql);
    }

    public static OperationResult Failed(string message)
    {
        return new OperationResult(false, null, message);
    }
}
