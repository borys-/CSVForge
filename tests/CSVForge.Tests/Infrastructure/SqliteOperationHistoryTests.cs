using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Operations;
using CSVForge.Domain.Imports;
using CSVForge.Domain.Operations;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteOperationHistoryTests
{
    [Fact]
    public async Task ListOperationsUseCase_ReturnsNewestOperationWithResultTable()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string csvPath = Path.Combine(directory, "data.csv");
        await File.WriteAllTextAsync(csvPath, "Id\r\n1\r\n1\r\n");

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(Path.Combine(directory, "workspace.db"));
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Data", true, null, null));
        OperationResult result = await provider.GetRequiredService<IFindDuplicatesUseCase>()
            .ExecuteAsync(new DuplicateSearchRequest(import.Import.TableName, ["Id"], DuplicateSearchMode.Summary, false));

        WorkspaceOperation operation = Assert.Single(await provider.GetRequiredService<IListOperationsUseCase>().ExecuteAsync());
        Assert.Equal("duplicates", operation.OperationType);
        Assert.Equal(result.ResultTableName, operation.ResultTableName);
    }
}
