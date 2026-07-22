using CSVForge.Application.Ports;
using CSVForge.Domain.Imports;
using CSVForge.Domain.Workspaces;
using CSVForge.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Workspaces;

internal sealed class SqliteWorkspaceService : IWorkspaceService
{
    public async Task<Workspace> CreateAsync(string workspacePath, CancellationToken cancellationToken)
    {
        string fullPath = NormalizeWorkspacePath(workspacePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory());

        await using SqliteConnection connection = SqliteConnectionFactory.Create(fullPath);
        await connection.OpenAsync(cancellationToken);
        await SqliteWorkspaceMigrator.MigrateAsync(connection, cancellationToken);

        Workspace workspace = CreateWorkspace(fullPath);
        await UpsertWorkspaceInfoAsync(connection, workspace, cancellationToken);

        return workspace;
    }

    public async Task<Workspace> OpenAsync(string workspacePath, CancellationToken cancellationToken)
    {
        string fullPath = NormalizeWorkspacePath(workspacePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Workspace file does not exist.", fullPath);
        }

        await using SqliteConnection connection = SqliteConnectionFactory.Create(fullPath);
        await connection.OpenAsync(cancellationToken);
        await SqliteWorkspaceMigrator.MigrateAsync(connection, cancellationToken);

        return await ReadWorkspaceAsync(connection, fullPath, cancellationToken) ?? CreateWorkspace(fullPath);
    }

    public async Task<IReadOnlyList<CsvImport>> ListImportsAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Array.Empty<CsvImport>();
    }

    private static string NormalizeWorkspacePath(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new ArgumentException("Workspace path is required.", nameof(workspacePath));
        }

        return Path.GetFullPath(workspacePath);
    }

    private static Workspace CreateWorkspace(string fullPath)
    {
        return new Workspace(fullPath, Path.GetFileNameWithoutExtension(fullPath), DateTimeOffset.UtcNow);
    }

    private static async Task UpsertWorkspaceInfoAsync(SqliteConnection connection, Workspace workspace, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO _workspace_info (key, value)
            VALUES ('name', $name), ('created_at', $createdAt)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$name", workspace.Name);
        command.Parameters.AddWithValue("$createdAt", workspace.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Workspace?> ReadWorkspaceAsync(SqliteConnection connection, string fullPath, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                MAX(CASE WHEN key = 'name' THEN value END) AS name,
                MAX(CASE WHEN key = 'created_at' THEN value END) AS created_at
            FROM _workspace_info
            WHERE key IN ('name', 'created_at');
            """;

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.IsDBNull(1))
        {
            return null;
        }

        string name = reader.GetString(0);
        DateTimeOffset createdAt = DateTimeOffset.Parse(reader.GetString(1));
        return new Workspace(fullPath, name, createdAt);
    }
}
