using System.Text;
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

    [Fact]
    public async Task ImportCsvUseCase_SkipsAndPersistsRowsWithWrongFieldCount()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "broken.csv");
        await File.WriteAllTextAsync(csvPath, "Name;Age\r\nAda;42\r\nOla\r\nZen;7;extra\r\n");

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Broken", true, null, null));

        Assert.Equal(1, result.Import.RowCount);
        Assert.Equal(2, result.Errors.Count);

        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM _workspace_errors WHERE import_id = $importId;";
        command.Parameters.AddWithValue("$importId", result.Import.Id.ToString());
        Assert.Equal(2L, (long)(await command.ExecuteScalarAsync() ?? 0L));
    }

    [Fact]
    public async Task ImportCsvUseCase_ThrowsForMissingFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(Path.Combine(directory, "workspace.db"));

        await Assert.ThrowsAsync<FileNotFoundException>(() => provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(Path.Combine(directory, "missing.csv"), "Missing", true, null, null)));
    }

    [Fact]
    public async Task ImportCsvUseCase_CancellationRemovesPartiallyImportedTable()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "large.csv");
        await File.WriteAllLinesAsync(csvPath, ["Id", .. Enumerable.Range(1, 1200).Select(value => value.ToString())]);

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        using CancellationTokenSource cancellation = new();
        CancelOnProgress progress = new(cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Large", true, null, null), progress, cancellation.Token));

        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name LIKE 'import_%';";
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync() ?? 0L));
        Assert.Empty(await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync());
    }

    [Fact]
    public async Task ImportCsvUseCase_AutomaticallyImportsWindows1250PolishText()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "polish.csv");
        await File.WriteAllTextAsync(csvPath, "Nazwa;Miasto\r\nŻółw;Łódź\r\n", Encoding.GetEncoding("windows-1250"));
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);

        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Polish", true, null, null));

        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT Miasto FROM \"{result.Import.TableName}\";";
        Assert.Equal("Łódź", (string?)await command.ExecuteScalarAsync());
    }

    private sealed class CancelOnProgress(CancellationTokenSource cancellation) : IProgress<ImportProgress>
    {
        public void Report(ImportProgress value)
        {
            if (value.ProcessedRows >= 500)
            {
                cancellation.Cancel();
            }
        }
    }
}
