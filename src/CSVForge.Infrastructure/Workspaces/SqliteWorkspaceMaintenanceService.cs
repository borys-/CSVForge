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
            await VerifyIntegrityAsync(fullOptimizedPath, cancellationToken);
            await using (SqliteConnection connection = SqliteConnectionFactory.Create(fullPath))
            {
                await connection.OpenAsync(cancellationToken);
                await using SqliteCommand checkpoint = connection.CreateCommand();
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await checkpoint.ExecuteNonQueryAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            SqliteConnection.ClearAllPools();
            string backupPath = fullPath + ".pre-optimize.bak";
            try
            {
                if (File.Exists(backupPath)) File.Delete(backupPath);
                File.Replace(fullOptimizedPath, fullPath, backupPath, ignoreMetadataErrors: true);
                TryDelete(fullPath + "-wal");
                TryDelete(fullPath + "-shm");
                return true;
            }
            catch (IOException)
            {
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

        await VerifyIntegrityAsync(connection, cancellationToken);
    }

    private static async Task VerifyIntegrityAsync(string path, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = SqliteConnectionFactory.Create(path);
        await connection.OpenAsync(cancellationToken);
        await VerifyIntegrityAsync(connection, cancellationToken);
    }

    private static async Task VerifyIntegrityAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
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

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
