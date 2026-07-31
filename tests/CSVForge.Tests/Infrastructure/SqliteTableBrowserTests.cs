using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Tables;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteTableBrowserTests
{
    [Fact]
    public async Task BrowseTableUseCase_RejectsUnsafeTableIdentifier()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(Path.Combine(directory, "workspace.db"));

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetRequiredService<IBrowseTableUseCase>()
            .ExecuteAsync(new BrowseTableRequest("people;DROP_TABLE", 10, 0, null, false, null)));
    }

    [Fact]
    public async Task BrowseTableUseCase_ReturnsPagedRowsAndMetadata()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "people.csv");
        await File.WriteAllTextAsync(csvPath, "Name;Age\r\nAda;42\r\nOla;7\r\nZen;99\r\n");

        ServiceProvider provider = new ServiceCollection()
            .AddApplication()
            .AddInfrastructure()
            .BuildServiceProvider();

        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null));

        IReadOnlyList<CsvImport> imports = await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync();
        TablePage page = await provider.GetRequiredService<IBrowseTableUseCase>()
            .ExecuteAsync(new BrowseTableRequest(import.Import.TableName, 2, 0, "Name", false, "a"));

        Assert.Single(imports);
        Assert.Equal(["Name", "Age"], imports[0].Columns.Select(column => column.Name));
        Assert.Equal(2, page.TotalRows);
        Assert.Equal(2, page.Rows.Count);
        Assert.Equal("Ada", page.Rows[0]["Name"]);
        Assert.Equal("Ola", page.Rows[1]["Name"]);
    }

    [Fact]
    public async Task BrowseTableUseCase_CombinesColumnFiltersAndReturnsDistinctValues()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "people.csv");
        await File.WriteAllTextAsync(csvPath,
            "Name;City;Status\r\nAda;Warszawa;Aktywny\r\nOla;Warszawa;Nieaktywny\r\nZen;Kraków;Aktywny\r\n");

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null));
        Dictionary<string, IReadOnlyList<string?>> filters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["City"] = ["Warszawa"],
            ["Status"] = ["Aktywny"]
        };

        TablePage page = await provider.GetRequiredService<IBrowseTableUseCase>()
            .ExecuteAsync(new BrowseTableRequest(
                import.Import.TableName, 10, 0, "Name", false, null, filters));

        Assert.Equal(1, page.TotalRows);
        Assert.Equal("Ada", page.Rows[0]["Name"]);

        IReadOnlyList<ColumnValueOption> statusValues = await provider.GetRequiredService<IGetColumnValuesUseCase>()
            .ExecuteAsync(new ColumnValuesRequest(
                import.Import.TableName, "Status", null, filters));
        Assert.Equal(2, statusValues.Count);
        Assert.Contains(statusValues, option => option.Value == "Aktywny" && option.Count == 1);
        Assert.Contains(statusValues, option => option.Value == "Nieaktywny" && option.Count == 1);
    }
}
