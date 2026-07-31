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

        var page = await provider.GetRequiredService<IExecuteSqlUseCase>().ExecuteAsync(result.Sql!);
        string sourceSql = result.Sql!.Trim().TrimEnd(';');
        var count = await provider.GetRequiredService<IExecuteSqlUseCase>()
            .ExecuteAsync($"SELECT COUNT(*) AS TotalRows FROM ({sourceSql}) AS _result;");
        var secondPage = await provider.GetRequiredService<IExecuteSqlUseCase>()
            .ExecuteAsync($"SELECT * FROM ({sourceSql}) AS _result LIMIT 1 OFFSET 1;");

        Assert.Equal(3, page.Rows.Count);
        Assert.Equal("3", count.Rows.Single()["TotalRows"]);
        Assert.Single(secondPage.Rows);
        Assert.Equal("b@example.com", secondPage.Rows[0]["Email"]);
        Assert.Contains(page.Rows, row => row["Email"] == "a@example.com" && row["status_porównania"] == "We wszystkich plikach");
        Assert.Contains(page.Rows, row => row["Email"] == "b@example.com" && row["status_porównania"] == "Tylko w: plik 1");
        Assert.Contains(page.Rows, row => row["Email"] == "c@example.com" && row["status_porównania"] == "Tylko w: plik 2");
        Assert.Contains(page.Rows, row => row["Email"] == "b@example.com" && row["plik1"] == "✓" && row["plik2"] == "");
        Assert.Contains(page.Rows, row => row["Email"] == "c@example.com" && row["plik1"] == "" && row["plik2"] == "✓");

        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand indexCommand = connection.CreateCommand();
        indexCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name LIKE 'idx_csvforge_%';";
        Assert.Equal(2L, (long)(await indexCommand.ExecuteScalarAsync() ?? 0L));
    }

    [Fact]
    public async Task CompareDatasetsUseCase_DifferentRowsReturnsBothExclusiveSides()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string leftPath = Path.Combine(directory, "left.csv");
        string rightPath = Path.Combine(directory, "right.csv");
        await File.WriteAllTextAsync(leftPath, "Id\r\n1\r\n2\r\n");
        await File.WriteAllTextAsync(rightPath, "Id\r\n1\r\n3\r\n");

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult left = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(leftPath, "Left", true, null, null));
        ImportResult right = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(rightPath, "Right", true, null, null));

        OperationResult result = await provider.GetRequiredService<ICompareDatasetsUseCase>()
            .ExecuteAsync(new DatasetCompareRequest(
                left.Import.TableName,
                right.Import.TableName,
                ["Id"],
                ["Id"],
                DatasetCompareMode.DifferentRows));
        var page = await provider.GetRequiredService<IExecuteSqlUseCase>().ExecuteAsync(result.Sql!);

        Assert.Equal(2, page.Rows.Count);
        Assert.Contains(page.Rows, row => row["Id"] == "2" && row["status_porównania"] == "Tylko w: plik 1");
        Assert.Contains(page.Rows, row => row["Id"] == "3" && row["status_porównania"] == "Tylko w: plik 2");
        Assert.DoesNotContain(page.Rows, row => row["Id"] == "1");
    }

    [Fact]
    public async Task CompareDatasetsUseCase_DescribesPresenceAcrossThreeFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string[] paths = [Path.Combine(directory, "a.csv"), Path.Combine(directory, "b.csv"), Path.Combine(directory, "c.csv")];
        await File.WriteAllTextAsync(paths[0], "Id\r\n1\r\n2\r\n4\r\n");
        await File.WriteAllTextAsync(paths[1], "Id\r\n2\r\n3\r\n4\r\n");
        await File.WriteAllTextAsync(paths[2], "Id\r\n2\r\n3\r\n5\r\n");

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult[] imports = new ImportResult[3];
        for (int index = 0; index < paths.Length; index++)
        {
            imports[index] = await provider.GetRequiredService<IImportCsvUseCase>()
                .ExecuteAsync(new ImportRequest(paths[index], $"Raport {index + 1}", true, null, null));
        }

        OperationResult result = await provider.GetRequiredService<ICompareDatasetsUseCase>()
            .ExecuteAsync(new DatasetCompareRequest(
            [
                new(imports[0].Import.TableName, "Raport 1", ["Id"]),
                new(imports[1].Import.TableName, "Raport 2", ["Id"]),
                new(imports[2].Import.TableName, "Raport 3", ["Id"])
            ], DatasetCompareMode.AllWithStatus));
        var page = await provider.GetRequiredService<IExecuteSqlUseCase>().ExecuteAsync(result.Sql!);

        Assert.Contains(page.Rows, row => row["Id"] == "1" && row["status_porównania"] == "Tylko w: plik 1");
        Assert.Contains(page.Rows, row => row["Id"] == "2" && row["status_porównania"] == "We wszystkich plikach");
        Assert.Contains(page.Rows, row => row["Id"] == "3" && row["status_porównania"] == "W plikach: plik 2, plik 3");
        Assert.Contains(page.Rows, row => row["Id"] == "4" && row["status_porównania"] == "W plikach: plik 1, plik 2");
        Assert.Contains(page.Rows, row => row["Id"] == "5" && row["status_porównania"] == "Tylko w: plik 3");
        Assert.Contains(page.Rows, row => row["Id"] == "3"
            && row["plik1"] == ""
            && row["plik2"] == "✓"
            && row["plik3"] == "✓");
    }
}
