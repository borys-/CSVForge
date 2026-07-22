using System.Text;
using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Csv;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class CsvReaderServiceTests
{
    [Fact]
    public async Task PreviewCsvUseCase_DetectsSemicolonDelimiterAndHeaders()
    {
        string csvPath = await WriteTempFileAsync("Name;City\r\nAda;Warszawa\r\nOla;Krakow\r\n", Encoding.UTF8);
        IPreviewCsvUseCase useCase = CreatePreviewUseCase();

        CsvPreview preview = await useCase.ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null));

        Assert.Equal(["Name", "City"], preview.Columns.Select(column => column.Name));
        Assert.Equal(2, preview.Rows.Count);
        Assert.Equal("Warszawa", preview.Rows[0][1]);
    }

    [Fact]
    public async Task PreviewCsvUseCase_GeneratesColumns_WhenFileHasNoHeader()
    {
        string csvPath = await WriteTempFileAsync("Ada,Warszawa\r\nOla,Krakow\r\n", Encoding.UTF8);
        IPreviewCsvUseCase useCase = CreatePreviewUseCase();

        CsvPreview preview = await useCase.ExecuteAsync(new ImportRequest(csvPath, "People", false, null, null));

        Assert.Equal(["Column1", "Column2"], preview.Columns.Select(column => column.Name));
        Assert.Equal(2, preview.Rows.Count);
        Assert.Equal("Ada", preview.Rows[0][0]);
    }

    [Fact]
    public async Task PreviewCsvUseCase_ReadsWindows1250()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding windows1250 = Encoding.GetEncoding("windows-1250");
        string csvPath = await WriteTempFileAsync("Nazwa;Miasto\r\nZazolc;Lodz\r\n", windows1250);
        IPreviewCsvUseCase useCase = CreatePreviewUseCase();

        CsvPreview preview = await useCase.ExecuteAsync(new ImportRequest(csvPath, "People", true, null, "windows-1250"));

        Assert.Equal("Lodz", preview.Rows[0][1]);
    }

    [Fact]
    public async Task PreviewCsvUseCase_NormalizesDuplicateHeaders()
    {
        string csvPath = await WriteTempFileAsync("Order Id;Order Id;123\r\n1;2;3\r\n", Encoding.UTF8);
        IPreviewCsvUseCase useCase = CreatePreviewUseCase();

        CsvPreview preview = await useCase.ExecuteAsync(new ImportRequest(csvPath, "Orders", true, null, null));

        Assert.Equal(["Order_Id", "Order_Id_2", "Column_123"], preview.Columns.Select(column => column.Name));
    }

    private static IPreviewCsvUseCase CreatePreviewUseCase()
    {
        ServiceProvider provider = new ServiceCollection()
            .AddApplication()
            .AddInfrastructure()
            .BuildServiceProvider();

        return provider.GetRequiredService<IPreviewCsvUseCase>();
    }

    private static async Task<string> WriteTempFileAsync(string content, Encoding encoding)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, "sample.csv");
        await File.WriteAllTextAsync(path, content, encoding);
        return path;
    }
}
