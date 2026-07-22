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
}
