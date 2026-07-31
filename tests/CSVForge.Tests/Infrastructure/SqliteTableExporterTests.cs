using System.Text;
using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Export;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteTableExporterTests
{
    [Fact]
    public async Task ExportTableUseCase_WritesUtf8BomHeaderAndEscapedRows()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string inputPath = Path.Combine(directory, "people.csv");
        string outputPath = Path.Combine(directory, "export.csv");
        await File.WriteAllTextAsync(inputPath, "Id;Name\r\n1;Łukasz\r\n2;\"Kowalski, Jan\"\r\n", new UTF8Encoding(true));

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(inputPath, "People", true, null, null));

        ExportResult result = await provider.GetRequiredService<IExportTableUseCase>()
            .ExecuteAsync(new ExportTableRequest(import.Import.TableName, outputPath, ',', true));

        byte[] bytes = await File.ReadAllBytesAsync(outputPath);
        string content = await File.ReadAllTextAsync(outputPath, Encoding.UTF8);
        Assert.Equal(2, result.ExportedRows);
        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.StartsWith("Id,Name", content);
        Assert.Contains("2,\"Kowalski, Jan\"", content);
        Assert.Contains("Łukasz", content);
    }

    [Fact]
    public async Task ExportTableUseCase_CancellationPreservesExistingOutput()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string inputPath = Path.Combine(directory, "input.csv");
        string outputPath = Path.Combine(directory, "output.csv");
        await File.WriteAllTextAsync(inputPath, "Id\r\n1\r\n");
        await File.WriteAllTextAsync(outputPath, "existing");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(inputPath, "Input", true, null, null));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<IExportTableUseCase>()
            .ExecuteAsync(new ExportTableRequest(import.Import.TableName, outputPath, ';', true), cancellation.Token));

        Assert.Equal("existing", await File.ReadAllTextAsync(outputPath));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task ExportTableUseCase_ExportsOnlySelectedColumns()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string inputPath = Path.Combine(directory, "input.csv");
        string outputPath = Path.Combine(directory, "output.csv");
        await File.WriteAllTextAsync(inputPath, "Id;Name;City\r\n1;Ada;Warszawa\r\n");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(inputPath, "Input", true, null, null));

        await provider.GetRequiredService<IExportTableUseCase>()
            .ExecuteAsync(new ExportTableRequest(
                import.Import.TableName,
                outputPath,
                ';',
                true,
                Columns: ["Name", "City"]));

        string content = await File.ReadAllTextAsync(outputPath);
        Assert.StartsWith("Name;City", content);
        Assert.Contains("Ada;Warszawa", content);
        Assert.DoesNotContain("Id;", content);
    }

    [Fact]
    public async Task ExportTableUseCase_ExportsFullSqlQueryResult()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string inputPath = Path.Combine(directory, "input.csv");
        string outputPath = Path.Combine(directory, "output.csv");
        await File.WriteAllTextAsync(inputPath, "Id;Name\r\n1;Ada\r\n2;Ola\r\n");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(inputPath, "Input", true, null, null));

        ExportResult result = await provider.GetRequiredService<IExportTableUseCase>()
            .ExecuteAsync(new ExportTableRequest(
                string.Empty,
                outputPath,
                ';',
                true,
                Columns: ["Name"],
                SourceSql: $"SELECT Name FROM \"{import.Import.TableName}\" WHERE Id = '2';"));

        Assert.Equal(1, result.ExportedRows);
        string content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("Ola", content);
        Assert.DoesNotContain("Ada", content);
    }

    [Fact]
    public async Task ExportTableUseCase_AppliesColumnFilters()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string inputPath = Path.Combine(directory, "input.csv");
        string outputPath = Path.Combine(directory, "output.csv");
        await File.WriteAllTextAsync(inputPath, "Name;Status\r\nAda;Aktywny\r\nOla;Nieaktywny\r\n");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>().ExecuteAsync(new ImportRequest(inputPath, "Input", true, null, null));

        ExportResult result = await provider.GetRequiredService<IExportTableUseCase>().ExecuteAsync(new ExportTableRequest(
            import.Import.TableName, outputPath, ';', true,
            ColumnFilters: new Dictionary<string, IReadOnlyList<string?>> { ["Status"] = ["Aktywny"] }));

        Assert.Equal(1, result.ExportedRows);
        string content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("Ada", content);
        Assert.DoesNotContain("Ola", content);
    }

}
