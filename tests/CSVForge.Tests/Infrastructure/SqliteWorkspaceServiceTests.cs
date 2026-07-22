using CSVForge.Application.Ports;
using CSVForge.Domain.Workspaces;
using CSVForge.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteWorkspaceServiceTests
{
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
        Assert.Contains("_workspace_operations", tables);
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
