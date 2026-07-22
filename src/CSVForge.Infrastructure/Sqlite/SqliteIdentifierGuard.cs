using CSVForge.Domain.Validation;

namespace CSVForge.Infrastructure.Sqlite;

internal static class SqliteIdentifierGuard
{
    public static void Table(string tableName) => DatabaseIdentifierValidator.EnsureValidTableName(tableName);

    public static void Columns(IEnumerable<string> columns)
    {
        foreach (string column in columns)
        {
            DatabaseIdentifierValidator.EnsureValidColumnName(column);
        }
    }
}
