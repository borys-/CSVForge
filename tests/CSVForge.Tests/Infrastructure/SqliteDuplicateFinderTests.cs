using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Tables;
using CSVForge.Domain.Imports;
using CSVForge.Domain.Operations;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteDuplicateFinderTests
{
    [Fact]
    public async Task FindDuplicatesUseCase_CreatesSummaryResultTable()
    {
        ServiceProvider provider = await CreateProviderWithImportedCsvAsync("Email;Name\r\na@example.com;Ada\r\nb@example.com;Ola\r\na@example.com;Adam\r\n");
        CsvImport import = (await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync()).Single();

        OperationResult result = await provider.GetRequiredService<IFindDuplicatesUseCase>()
            .ExecuteAsync(new DuplicateSearchRequest(import.TableName, ["Email"], DuplicateSearchMode.Summary, true));

        Assert.True(result.Success);

        TablePage page = await provider.GetRequiredService<IBrowseTableUseCase>()
            .ExecuteAsync(new BrowseTableRequest(result.ResultTableName!, 10, 0, null, false, null));

        Assert.Single(page.Rows);
        Assert.Equal("a@example.com", page.Rows[0]["Email"]);
        Assert.Equal("2", page.Rows[0]["duplicate_count"]);
    }

    [Fact]
    public async Task FindDuplicatesUseCase_CreatesDuplicateRowsResultTable()
    {
        ServiceProvider provider = await CreateProviderWithImportedCsvAsync("Email;Name\r\na@example.com;Ada\r\nb@example.com;Ola\r\na@example.com;Adam\r\n");
        CsvImport import = (await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync()).Single();

        OperationResult result = await provider.GetRequiredService<IFindDuplicatesUseCase>()
            .ExecuteAsync(new DuplicateSearchRequest(import.TableName, ["Email"], DuplicateSearchMode.AllDuplicateRows, true));

        TablePage page = await provider.GetRequiredService<IBrowseTableUseCase>()
            .ExecuteAsync(new BrowseTableRequest(result.ResultTableName!, 10, 0, "Name", false, null));

        Assert.Equal(2, page.Rows.Count);
        Assert.Equal("Ada", page.Rows[0]["Name"]);
        Assert.Equal("Adam", page.Rows[1]["Name"]);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task FindDuplicatesUseCase_HandlesEmptyKeysAccordingToRequest(bool ignoreEmptyValues, int expectedRows)
    {
        ServiceProvider provider = await CreateProviderWithImportedCsvAsync(
            "Email;Name\r\n;Ada\r\n;Ola\r\nvalid@example.com;Jan\r\n");
        CsvImport import = (await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync()).Single();

        OperationResult result = await provider.GetRequiredService<IFindDuplicatesUseCase>()
            .ExecuteAsync(new DuplicateSearchRequest(import.TableName, ["Email"], DuplicateSearchMode.Summary, ignoreEmptyValues));

        TablePage page = await provider.GetRequiredService<IBrowseTableUseCase>()
            .ExecuteAsync(new BrowseTableRequest(result.ResultTableName!, 10, 0, null, false, null));

        Assert.Equal(expectedRows, page.Rows.Count);
        if (!ignoreEmptyValues)
        {
            Assert.Equal("2", page.Rows[0]["duplicate_count"]);
        }
    }

    private static async Task<ServiceProvider> CreateProviderWithImportedCsvAsync(string content)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "people.csv");
        await File.WriteAllTextAsync(csvPath, content);

        ServiceProvider provider = new ServiceCollection()
            .AddApplication()
            .AddInfrastructure()
            .BuildServiceProvider();

        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        await provider.GetRequiredService<IImportCsvUseCase>().ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null));

        return provider;
    }
}
