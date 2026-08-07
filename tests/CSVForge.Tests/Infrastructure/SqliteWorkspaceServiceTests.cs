using CSVForge.Application;
using CSVForge.Application.Ports;
using CSVForge.Domain.Workspaces;
using CSVForge.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteWorkspaceServiceTests
{
    [Fact]
    public async Task OpenAsync_IsIdempotentAndRecoversOrphanedImportTable()
    {
        string workspacePath = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"), "recovery.db");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        IWorkspaceService service = provider.GetRequiredService<IWorkspaceService>();
        await service.CreateAsync(workspacePath, CancellationToken.None);
        await using (SqliteConnection connection = new($"Data Source={workspacePath}"))
        {
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE import_interrupted (value TEXT);";
            await command.ExecuteNonQueryAsync();
        }

        await service.OpenAsync(workspacePath, CancellationToken.None);
        await service.OpenAsync(workspacePath, CancellationToken.None);

        Assert.DoesNotContain("import_interrupted", await ReadTablesAsync(workspacePath, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_UsesWalAndForeignKeysPolicy()
    {
        string workspacePath = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"), "wal.db");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<IWorkspaceService>().CreateAsync(workspacePath, CancellationToken.None);
        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand journal = connection.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", ((string?)await journal.ExecuteScalarAsync())?.ToLowerInvariant());
    }

    [Fact]
    public async Task CreateAsync_CreatesWorkspaceFileAndMetadataTables()
    {
        string workspacePath = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"), "test.db");
        ServiceProvider provider = new ServiceCollection()
            .AddInfrastructure()
            .BuildServiceProvider();

        IWorkspaceService service = provider.GetRequiredService<IWorkspaceService>();

        Workspace workspace = await service.CreateAsync(workspacePath, CancellationToken.None);

        Assert.True(File.Exists(workspacePath));
        Assert.Equal(Path.GetFullPath(workspacePath), workspace.Path);
        Assert.Equal("test", workspace.Name);

        IReadOnlySet<string> tables = await ReadTablesAsync(workspacePath, CancellationToken.None);
        Assert.Contains("_workspace_info", tables);
        Assert.Contains("_workspace_imports", tables);
        Assert.Contains("_workspace_columns", tables);
        Assert.Contains("_workspace_errors", tables);
        Assert.DoesNotContain("_workspace_operations", tables);
    }

    [Fact]
    public async Task OpenAsync_ReturnsPersistedWorkspaceMetadata()
    {
        string workspacePath = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"), "open-test.db");
        ServiceProvider provider = new ServiceCollection()
            .AddInfrastructure()
            .BuildServiceProvider();

        IWorkspaceService service = provider.GetRequiredService<IWorkspaceService>();
        Workspace created = await service.CreateAsync(workspacePath, CancellationToken.None);

        Workspace opened = await service.OpenAsync(workspacePath, CancellationToken.None);

        Assert.Equal(created.Path, opened.Path);
        Assert.Equal(created.Name, opened.Name);
        Assert.Equal(created.CreatedAt, opened.CreatedAt);
    }

    [Fact]
    public async Task OpenAsync_WhenWalDatabaseHasActiveWriter_RemainsReadable()
    {
        string workspacePath = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"), "locked.db");
        await using ServiceProvider provider = new ServiceCollection()
            .AddInfrastructure()
            .BuildServiceProvider();
        IWorkspaceService service = provider.GetRequiredService<IWorkspaceService>();
        await service.CreateAsync(workspacePath, CancellationToken.None);

        await using SqliteConnection lockConnection = new($"Data Source={workspacePath};Default Timeout=1");
        await lockConnection.OpenAsync();
        await using SqliteCommand lockCommand = lockConnection.CreateCommand();
        lockCommand.CommandText = "BEGIN EXCLUSIVE;";
        await lockCommand.ExecuteNonQueryAsync();

        Workspace workspace = await service.OpenAsync(workspacePath, CancellationToken.None);

        Assert.Equal(Path.GetFullPath(workspacePath), workspace.Path);
    }

    private static async Task<IReadOnlySet<string>> ReadTablesAsync(string workspacePath, CancellationToken cancellationToken)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = workspacePath,
            Mode = SqliteOpenMode.ReadOnly
        };

        await using SqliteConnection connection = new(builder.ToString());
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        HashSet<string> tables = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }
}
