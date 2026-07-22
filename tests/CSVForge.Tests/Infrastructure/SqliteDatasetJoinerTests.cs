using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Tables;
using CSVForge.Domain.Imports;
using CSVForge.Domain.Operations;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteDatasetJoinerTests
{
    [Theory]
    [InlineData(DatasetJoinType.Inner, 1)]
    [InlineData(DatasetJoinType.Left, 2)]
    [InlineData(DatasetJoinType.Right, 2)]
    public async Task JoinDatasetsUseCase_CreatesExpectedRows(DatasetJoinType joinType, int expectedRows)
    {
        (ServiceProvider provider, ImportResult left, ImportResult right) = await CreateWorkspaceAsync();
        OperationResult result = await provider.GetRequiredService<IJoinDatasetsUseCase>().ExecuteAsync(new DatasetJoinRequest(
            left.Import.TableName, right.Import.TableName, ["Id"], ["Id"], ["Id", "Name"], ["Id", "City"], joinType));

        TablePage page = await provider.GetRequiredService<IBrowseTableUseCase>()
            .ExecuteAsync(new BrowseTableRequest(result.ResultTableName!, 10, 0, null, false, null));

        Assert.Equal(expectedRows, page.Rows.Count);
        Assert.Contains("right_Id", page.Columns);
        Assert.Contains(page.Rows, row => row["Id"] == "2" && row["City"] == "Warszawa");
        if (joinType == DatasetJoinType.Left)
        {
            Assert.Contains(page.Rows, row => row["Id"] == "1" && row["City"] is null);
        }
        if (joinType == DatasetJoinType.Right)
        {
            Assert.Contains(page.Rows, row => row["right_Id"] == "3" && row["Name"] is null);
        }
    }

    private static async Task<(ServiceProvider Provider, ImportResult Left, ImportResult Right)> CreateWorkspaceAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string leftPath = Path.Combine(directory, "left.csv");
        string rightPath = Path.Combine(directory, "right.csv");
        await File.WriteAllTextAsync(leftPath, "Id;Name\r\n1;Ada\r\n2;Ola\r\n");
        await File.WriteAllTextAsync(rightPath, "Id;City\r\n2;Warszawa\r\n3;Kraków\r\n");

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult left = await provider.GetRequiredService<IImportCsvUseCase>().ExecuteAsync(new ImportRequest(leftPath, "Left", true, null, null));
        ImportResult right = await provider.GetRequiredService<IImportCsvUseCase>().ExecuteAsync(new ImportRequest(rightPath, "Right", true, null, null));
        return (provider, left, right);
    }
}
