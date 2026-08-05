using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteWorkspaceMaintenanceTests
{
    [Fact]
    public async Task Replace_RejectsCorruptedOptimizedCopyAndPreservesWorkspace()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        string corruptPath = Path.Combine(directory, "corrupt.tmp");
        await File.WriteAllTextAsync(corruptPath, "not a sqlite database");

        Assert.False(await provider.GetRequiredService<IWorkspaceMaintenanceService>()
            .TryReplaceWithOptimizedCopyAsync(workspacePath, corruptPath, CancellationToken.None));

        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check;";
        Assert.Equal("ok", (string?)await integrity.ExecuteScalarAsync());
    }

    [Fact]
    public async Task OptimizedCopy_RemainsSeparateUntilItAtomicallyReplacesWorkspace()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "data.csv");
        await File.WriteAllLinesAsync(csvPath, ["Id;Name", .. Enumerable.Range(1, 2_000).Select(index => $"{index};Person {index}")]);

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null));
        await provider.GetRequiredService<IDeleteImportUseCase>().ExecuteAsync(import.Import.Id);

        IWorkspaceMaintenanceService maintenance = provider.GetRequiredService<IWorkspaceMaintenanceService>();
        string optimizedPath = await maintenance.PrepareOptimizedCopyAsync(workspacePath, CancellationToken.None);
        try
        {
            Assert.True(File.Exists(workspacePath));
            Assert.True(File.Exists(optimizedPath));
            Assert.Empty(await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync());

            Assert.True(await maintenance.TryReplaceWithOptimizedCopyAsync(workspacePath, optimizedPath, CancellationToken.None));
            Assert.False(File.Exists(optimizedPath));
            Assert.True(File.Exists(workspacePath + ".pre-optimize.bak"));

            await using SqliteConnection connection = new($"Data Source={workspacePath}");
            await connection.OpenAsync();
            await using SqliteCommand integrity = connection.CreateCommand();
            integrity.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", (string?)await integrity.ExecuteScalarAsync());
        }
        finally
        {
            maintenance.DiscardOptimizedCopy(optimizedPath);
        }
    }
}
