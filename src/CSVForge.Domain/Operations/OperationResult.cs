namespace CSVForge.Domain.Operations;

public sealed record OperationResult(
    bool Success,
    string? ResultTableName,
    string Message)
{
    public static OperationResult Ok(string resultTableName, string message)
    {
        return new OperationResult(true, resultTableName, message);
    }

    public static OperationResult Failed(string message)
    {
        return new OperationResult(false, null, message);
    }
}
