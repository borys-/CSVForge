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
    public async Task PreviewCsvUseCase_AutomaticallyDetectsWindows1250()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string csvPath = await WriteTempFileAsync("Nazwa;Miasto\r\nŻółw;Łódź\r\n", Encoding.GetEncoding("windows-1250"));

        CsvPreview preview = await CreatePreviewUseCase().ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null));

        Assert.Equal("Żółw", preview.Rows[0][0]);
        Assert.Equal("Łódź", preview.Rows[0][1]);
    }

    [Fact]
    public async Task PreviewCsvUseCase_DetectsTabDelimiter()
    {
        string csvPath = await WriteTempFileAsync("Name\tCity\r\nAda\tWarszawa\r\n", Encoding.UTF8);

        CsvPreview preview = await CreatePreviewUseCase().ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null));

        Assert.Equal(["Name", "City"], preview.Columns.Select(column => column.Name));
        Assert.Equal("Warszawa", preview.Rows[0][1]);
    }

    [Fact]
    public async Task PreviewCsvUseCase_ReadsUtf8Bom()
    {
        string csvPath = await WriteTempFileAsync("Nazwa;Miasto\r\nŻółw;Łódź\r\n", new UTF8Encoding(true));

        CsvPreview preview = await CreatePreviewUseCase().ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null));

        Assert.Equal("Nazwa", preview.Columns[0].Name);
        Assert.Equal("Łódź", preview.Rows[0][1]);
    }

    [Fact]
    public async Task PreviewCsvUseCase_ReturnsEmptyPreviewForEmptyFile()
    {
        string csvPath = await WriteTempFileAsync(string.Empty, Encoding.UTF8);

        CsvPreview preview = await CreatePreviewUseCase().ExecuteAsync(new ImportRequest(csvPath, "Empty", true, null, null));

        Assert.Empty(preview.Columns);
        Assert.Empty(preview.Rows);
        Assert.Empty(preview.Errors);
    }

    [Fact]
    public async Task PreviewCsvUseCase_AutoDetectsHeader()
    {
        string csvPath = await WriteTempFileAsync("Name;Age\r\nAda;42\r\n", Encoding.UTF8);

        CsvPreview preview = await CreatePreviewUseCase().ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null, 500, true));

        Assert.Equal(["Name", "Age"], preview.Columns.Select(column => column.Name));
        Assert.Single(preview.Rows);
    }

    [Fact]
    public async Task PreviewCsvUseCase_AutoDetectsTextRowsWithoutHeader()
    {
        string csvPath = await WriteTempFileAsync("Ada;Warszawa\r\nOla;Krakow\r\n", Encoding.UTF8);

        CsvPreview preview = await CreatePreviewUseCase().ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null, 500, true));

        Assert.Equal(["Column1", "Column2"], preview.Columns.Select(column => column.Name));
        Assert.Equal(2, preview.Rows.Count);
    }

    [Fact]
    public async Task PreviewCsvUseCase_SkipsReportPreambleAndTrailingDelimiter()
    {
        string csvPath = await WriteTempFileAsync(
            "Obiekty: od 1 do 3 ze wszystkich 3\r\nPPE;\r\n590380100003453588;\r\n590380100012575219;\r\n",
            Encoding.UTF8);

        CsvPreview preview = await CreatePreviewUseCase().ExecuteAsync(
            new ImportRequest(csvPath, "Energy", true, null, null, 500, true));

        Assert.Equal(["PPE"], preview.Columns.Select(column => column.Name));
        Assert.Equal(2, preview.Rows.Count);
        Assert.Equal("590380100003453588", preview.Rows[0][0]);
        Assert.All(preview.Rows, row => Assert.Single(row));
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
