namespace CSVForge.Infrastructure.Sqlite;

internal static class SqliteIdentifier
{
    public static string Quote(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
