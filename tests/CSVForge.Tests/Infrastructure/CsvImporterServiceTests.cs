using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class CsvImporterServiceTests
{
    [Fact]
    public async Task ImportCsvUseCase_ImportsRowsAsTextAndStoresMetadata()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "people.csv");
        await File.WriteAllTextAsync(csvPath, "Name;Age\r\nAda;42\r\nOla;7\r\n");

        ServiceProvider provider = new ServiceCollection()
            .AddApplication()
            .AddInfrastructure()
            .BuildServiceProvider();

        ICreateWorkspaceUseCase createWorkspace = provider.GetRequiredService<ICreateWorkspaceUseCase>();
        IImportCsvUseCase importCsv = provider.GetRequiredService<IImportCsvUseCase>();

        await createWorkspace.ExecuteAsync(workspacePath);
        ImportResult result = await importCsv.ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null));

        Assert.Equal(2, result.Import.RowCount);
        Assert.Equal(["Name", "Age"], result.Import.Columns.Select(column => column.Name));

        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();

        await using SqliteCommand rowCountCommand = connection.CreateCommand();
        rowCountCommand.CommandText = $"SELECT COUNT(*) FROM \"{result.Import.TableName}\";";
        Assert.Equal(2L, (long)(await rowCountCommand.ExecuteScalarAsync() ?? 0L));

        await using SqliteCommand metadataCommand = connection.CreateCommand();
        metadataCommand.CommandText = "SELECT COUNT(*) FROM _workspace_imports WHERE id = $id;";
        metadataCommand.Parameters.AddWithValue("$id", result.Import.Id.ToString());
        Assert.Equal(1L, (long)(await metadataCommand.ExecuteScalarAsync() ?? 0L));
    }
}
