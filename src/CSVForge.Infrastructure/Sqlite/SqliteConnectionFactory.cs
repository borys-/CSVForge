using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Sqlite;

internal static class SqliteConnectionFactory
{
    public static SqliteConnection Create(string workspacePath)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = workspacePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        return new SqliteConnection(builder.ToString());
    }
}
