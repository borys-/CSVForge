using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Tables;
using CSVForge.Domain.Imports;
using CSVForge.Domain.Operations;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteDatasetComparerTests
{
    [Fact]
    public async Task CompareDatasetsUseCase_CreatesStatusResultTable()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string workspacePath = Path.Combine(directory, "workspace.db");
        string leftPath = Path.Combine(directory, "left.csv");
        string rightPath = Path.Combine(directory, "right.csv");
        await File.WriteAllTextAsync(leftPath, "Email;Name\r\na@example.com;Ada\r\nb@example.com;Ola\r\n");
        await File.WriteAllTextAsync(rightPath, "Email;Name\r\na@example.com;Ada\r\nc@example.com;Zen\r\n");

        ServiceProvider provider = new ServiceCollection()
            .AddApplication()
            .AddInfrastructure()
            .BuildServiceProvider();

        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult left = await provider.GetRequiredService<IImportCsvUseCase>().ExecuteAsync(new ImportRequest(leftPath, "Left", true, null, null));
        ImportResult right = await provider.GetRequiredService<IImportCsvUseCase>().ExecuteAsync(new ImportRequest(rightPath, "Right", true, null, null));

        OperationResult result = await provider.GetRequiredService<ICompareDatasetsUseCase>()
            .ExecuteAsync(new DatasetCompareRequest(
                left.Import.TableName,
                right.Import.TableName,
                ["Email"],
                ["Email"],
                DatasetCompareMode.AllWithStatus));

        TablePage page = await provider.GetRequiredService<IBrowseTableUseCase>()
            .ExecuteAsync(new BrowseTableRequest(result.ResultTableName!, 10, 0, "Email", false, null));

        Assert.Equal(3, page.Rows.Count);
        Assert.Contains(page.Rows, row => row["Email"] == "a@example.com" && row["compare_status"] == "common");
        Assert.Contains(page.Rows, row => row["Email"] == "b@example.com" && row["compare_status"] == "left_only");
        Assert.Contains(page.Rows, row => row["Email"] == "c@example.com" && row["compare_status"] == "right_only");

        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand indexCommand = connection.CreateCommand();
        indexCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name LIKE 'idx_csvforge_%';";
        Assert.Equal(2L, (long)(await indexCommand.ExecuteScalarAsync() ?? 0L));
    }
}
