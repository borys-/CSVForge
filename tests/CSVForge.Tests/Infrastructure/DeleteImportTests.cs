using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class DeleteImportTests
{
    [Fact]
    public async Task DeleteImportUseCase_RemovesTableAndMetadata()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "data.csv");
        await File.WriteAllLinesAsync(
            csvPath,
            ["Id;Payload", .. Enumerable.Range(1, 5_000).Select(index => $"{index};{new string('x', 200)}")]);
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Data", true, null, null));

        await provider.GetRequiredService<IDeleteImportUseCase>().ExecuteAsync(import.Import.Id);

        Assert.Empty(await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync());
        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", import.Import.TableName);
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync() ?? 0L));

        command.Parameters.Clear();
        command.CommandText = "PRAGMA freelist_count;";
        Assert.True((long)(await command.ExecuteScalarAsync() ?? 0L) > 0L);
    }
}
