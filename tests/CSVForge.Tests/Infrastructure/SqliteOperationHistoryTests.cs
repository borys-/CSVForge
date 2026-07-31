using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Operations;
using CSVForge.Application.Tables;
using CSVForge.Domain.Imports;
using CSVForge.Domain.Operations;
using CSVForge.Infrastructure;
using Microsoft.Data.Sqlite;
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

        Assert.Null(result.ResultTableName);
        Assert.StartsWith("SELECT", result.Sql!.TrimStart(), StringComparison.OrdinalIgnoreCase);
        WorkspaceOperation operation = Assert.Single(await provider.GetRequiredService<IListOperationsUseCase>().ExecuteAsync());
        Assert.Equal("duplicates", operation.OperationType);
        Assert.Null(operation.ResultTableName);
        Assert.Equal(result.Sql, operation.SourceSql);

        await using SqliteConnection connection = new($"Data Source={Path.Combine(directory, "workspace.db")}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND (name LIKE '_compare_%' OR name LIKE '_join_%' OR name LIKE '_duplicates_%');
            """;
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync() ?? -1L));
    }

    [Fact]
    public async Task DeleteOperationUseCase_RemovesHistoryAndResultTable()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string csvPath = Path.Combine(directory, "data.csv");
        await File.WriteAllTextAsync(csvPath, "Id\r\n1\r\n1\r\n");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(Path.Combine(directory, "workspace.db"));
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Data", true, null, null));
        await provider.GetRequiredService<IFindDuplicatesUseCase>()
            .ExecuteAsync(new DuplicateSearchRequest(import.Import.TableName, ["Id"], DuplicateSearchMode.Summary, false));
        WorkspaceOperation operation = Assert.Single(await provider.GetRequiredService<IListOperationsUseCase>().ExecuteAsync());

        await provider.GetRequiredService<IDeleteOperationUseCase>().ExecuteAsync(operation.Id);

        Assert.Empty(await provider.GetRequiredService<IListOperationsUseCase>().ExecuteAsync());
        Assert.NotNull(operation.SourceSql);
        Assert.Single(await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync());
    }
}
