using System.IO.Compression;
using CSVForge.Infrastructure.Csv;

namespace CSVForge.Tests.Infrastructure;

public sealed class ZipCsvExtractorTests
{
    [Fact]
    public async Task ExtractAsync_ExtractsCsvFilesIncludingNestedAndUppercaseEntries()
    {
        string archivePath = CreateArchive(
            ("first.csv", "Id;Name\r\n1;Ada\r\n"),
            ("nested/SECOND.CSV", "Id;Name\r\n2;Ola\r\n"),
            ("readme.txt", "ignored"));

        IReadOnlyList<ZipCsvExtractor.ExtractedCsvFile> files = await ZipCsvExtractor.ExtractAsync(archivePath);
        try
        {
            Assert.Equal(["first.csv", "nested/SECOND.CSV"], files.Select(file => file.EntryName));
            Assert.Equal("Id;Name\r\n1;Ada\r\n", await File.ReadAllTextAsync(files[0].FilePath));
            Assert.Equal("Id;Name\r\n2;Ola\r\n", await File.ReadAllTextAsync(files[1].FilePath));
        }
        finally
        {
            foreach (ZipCsvExtractor.ExtractedCsvFile file in files) file.Dispose();
        }
    }

    [Fact]
    public async Task ExtractAsync_RejectsArchiveWithoutCsvFiles()
    {
        string archivePath = CreateArchive(("readme.txt", "nothing to import"));

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(
            () => ZipCsvExtractor.ExtractAsync(archivePath));

        Assert.Contains("nie zawiera", error.Message);
    }

    private static string CreateArchive(params (string Name, string Content)[] entries)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string archivePath = Path.Combine(directory, "input.zip");
        using ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach ((string name, string content) in entries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using StreamWriter writer = new(entry.Open());
            writer.Write(content);
        }
        return archivePath;
    }
}
