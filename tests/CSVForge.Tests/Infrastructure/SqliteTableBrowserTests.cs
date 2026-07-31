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
            .ExecuteAsync(new BrowseTableRequest("people;DROP_TABLE", 10, 0, null, false)));
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
            .ExecuteAsync(new BrowseTableRequest(import.Import.TableName, 2, 0, "Name", false));

        Assert.Single(imports);
        Assert.Equal(["Name", "Age"], imports[0].Columns.Select(column => column.Name));
        Assert.Equal(3, page.TotalRows);
        Assert.Equal(2, page.Rows.Count);
        Assert.Equal("Ada", page.Rows[0]["Name"]);
        Assert.Equal("Ola", page.Rows[1]["Name"]);
    }

}
