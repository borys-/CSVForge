using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Sqlite;

internal static class SqliteDatabaseMaintenance
{
    public static Task OptimizeAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken) =>
        ExecuteAsync(connection, "PRAGMA optimize;", cancellationToken);

    public static Task CompactAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            """
            PRAGMA busy_timeout = 15000;
            VACUUM;
            PRAGMA optimize;
            """,
            cancellationToken);

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
