using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Sqlite;

internal static class SqliteWorkspaceMigrator
{
    public static async Task MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        string sql = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS _workspace_info (
                key TEXT PRIMARY KEY NOT NULL,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS _workspace_imports (
                id TEXT PRIMARY KEY NOT NULL,
                display_name TEXT NOT NULL,
                source_path TEXT NOT NULL,
                table_name TEXT NOT NULL UNIQUE,
                imported_at TEXT NOT NULL,
                row_count INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS _workspace_columns (
                import_id TEXT NOT NULL,
                column_index INTEGER NOT NULL,
                original_name TEXT NOT NULL,
                name TEXT NOT NULL,
                PRIMARY KEY (import_id, column_index),
                FOREIGN KEY (import_id) REFERENCES _workspace_imports(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS _workspace_errors (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                import_id TEXT,
                row_number INTEGER NOT NULL,
                message TEXT NOT NULL,
                raw_row TEXT,
                FOREIGN KEY (import_id) REFERENCES _workspace_imports(id) ON DELETE CASCADE
            );

            DROP TABLE IF EXISTS _workspace_operations;
            """;

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await RemoveInterruptedImportsAsync(connection, cancellationToken);
    }

    private static async Task RemoveInterruptedImportsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand find = connection.CreateCommand();
        find.CommandText = """
            SELECT name FROM sqlite_master
            WHERE type = 'table' AND name LIKE 'import_%'
              AND name NOT IN (SELECT table_name FROM _workspace_imports);
            """;
        List<string> orphanedTables = [];
        await using (SqliteDataReader reader = await find.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) orphanedTables.Add(reader.GetString(0));
        }

        foreach (string tableName in orphanedTables)
        {
            await using SqliteCommand drop = connection.CreateCommand();
            drop.CommandText = $"DROP TABLE {SqliteIdentifier.Quote(tableName)};";
            await drop.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
