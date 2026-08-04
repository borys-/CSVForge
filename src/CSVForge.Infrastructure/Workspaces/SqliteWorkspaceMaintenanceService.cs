using CSVForge.Application.Ports;
using CSVForge.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Workspaces;

internal sealed class SqliteWorkspaceMaintenanceService : IWorkspaceMaintenanceService
{
    public async Task<string> PrepareOptimizedCopyAsync(string workspacePath, CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(workspacePath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Workspace file does not exist.", fullPath);

        string directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        string optimizedPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.optimize-{Guid.NewGuid():N}.tmp");
        try
        {
            await CreateConsistentCopyAsync(fullPath, optimizedPath, cancellationToken);
            await OptimizeCopyAsync(optimizedPath, cancellationToken);
            return optimizedPath;
        }
        catch
        {
            DiscardOptimizedCopy(optimizedPath);
            throw;
        }
    }

    public async Task<bool> TryReplaceWithOptimizedCopyAsync(
        string workspacePath,
        string optimizedCopyPath,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(workspacePath);
        string fullOptimizedPath = Path.GetFullPath(optimizedCopyPath);
        if (!File.Exists(fullPath) || !File.Exists(fullOptimizedPath)) return false;

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using (SqliteConnection connection = SqliteConnectionFactory.Create(fullPath))
            {
                await connection.OpenAsync(cancellationToken);
                await using SqliteCommand checkpoint = connection.CreateCommand();
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await checkpoint.ExecuteNonQueryAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            SqliteConnection.ClearAllPools();
            string backupPath = fullPath + $".pre-optimize-{Guid.NewGuid():N}.bak";
            try
            {
                File.Replace(fullOptimizedPath, fullPath, backupPath, ignoreMetadataErrors: true);
                TryDelete(backupPath);
                TryDelete(fullPath + "-wal");
                TryDelete(fullPath + "-shm");
                return true;
            }
            catch (IOException)
            {
                TryDelete(backupPath);
                return false;
            }
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    public void DiscardOptimizedCopy(string optimizedCopyPath)
    {
        SqliteConnection.ClearAllPools();
        TryDelete(optimizedCopyPath);
        TryDelete(optimizedCopyPath + "-wal");
        TryDelete(optimizedCopyPath + "-shm");
    }

    private static async Task CreateConsistentCopyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection source = SqliteConnectionFactory.Create(sourcePath);
        await source.OpenAsync(cancellationToken);
        await using SqliteConnection destination = SqliteConnectionFactory.Create(destinationPath);
        await destination.OpenAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        source.BackupDatabase(destination);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task OptimizeCopyAsync(string path, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = SqliteConnectionFactory.Create(path);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, "PRAGMA journal_mode = DELETE;", cancellationToken);
        await ExecuteAsync(connection, "PRAGMA temp_store = MEMORY;", cancellationToken);
        await ExecuteAsync(connection, "VACUUM;", cancellationToken);
        await ExecuteAsync(connection, "ANALYZE;", cancellationToken);
        await ExecuteAsync(connection, "PRAGMA optimize;", cancellationToken);

        await using SqliteCommand integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check;";
        string? result = (string?)await integrity.ExecuteScalarAsync(cancellationToken);
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Optimized workspace failed integrity check: {result ?? "no result"}.");
        }
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
