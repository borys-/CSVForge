using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Export;
using CSVForge.Application.Tables;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteTableMaterializerTests
{
    [Fact]
    public async Task CreateTableFromResult_CopiesSelectedColumnsAndRegistersHistory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string inputPath = Path.Combine(directory, "input.csv");
        await File.WriteAllTextAsync(inputPath, "Id;Name;City\r\n1;Ada;Warszawa\r\n2;Ola;Kraków\r\n");

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(inputPath, "Input", true, null, null));

        CreateTableFromResultResult result = await provider.GetRequiredService<ICreateTableFromResultUseCase>()
            .ExecuteAsync(new CreateTableFromResultRequest(
                import.Import.TableName,
                "wybrane_osoby",
                ["Name"],
                "Ada"));

        Assert.Equal(1, result.RowCount);
        TablePage page = await provider.GetRequiredService<IBrowseTableUseCase>()
            .ExecuteAsync(new BrowseTableRequest(result.TableName, 100, 0, null, false, null));
        Assert.Equal(["Name"], page.Columns);
        Assert.Single(page.Rows);
        Assert.Equal("Ada", page.Rows[0]["Name"]);

        IReadOnlyList<CsvImport> imports = await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync();
        Assert.Contains(imports, item => item.TableName == "wybrane_osoby" && item.RowCount == 1);
    }

    [Fact]
    public async Task CreateTableFromResult_MaterializesSqlQuery()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string inputPath = Path.Combine(directory, "input.csv");
        await File.WriteAllTextAsync(inputPath, "Id;Name\r\n1;Ada\r\n2;Ola\r\n");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(inputPath, "Input", true, null, null));

        CreateTableFromResultResult result = await provider.GetRequiredService<ICreateTableFromResultUseCase>()
            .ExecuteAsync(new CreateTableFromResultRequest(
                string.Empty,
                "sql_wynik",
                ["Name"],
                SourceSql: $"SELECT Name FROM \"{import.Import.TableName}\" WHERE Id = '1';"));

        Assert.Equal(1, result.RowCount);
        TablePage page = await provider.GetRequiredService<IBrowseTableUseCase>()
            .ExecuteAsync(new BrowseTableRequest(result.TableName, 100, 0, null, false, null));
        Assert.Equal("Ada", page.Rows[0]["Name"]);
    }
}
