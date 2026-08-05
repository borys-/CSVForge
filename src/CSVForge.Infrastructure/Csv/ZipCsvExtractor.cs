using System.IO.Compression;

namespace CSVForge.Infrastructure.Csv;

public static class ZipCsvExtractor
{
    public static async Task<IReadOnlyList<ExtractedCsvFile>> ExtractAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("ZIP file does not exist.", archivePath);
        }

        List<ExtractedCsvFile> extractedFiles = [];
        try
        {
            await using FileStream stream = File.OpenRead(archivePath);
            using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);
            ZipArchiveEntry[] csvEntries = archive.Entries
                .Where(entry => !string.IsNullOrEmpty(entry.Name)
                    && string.Equals(Path.GetExtension(entry.Name), ".csv", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (csvEntries.Length == 0)
            {
                throw new InvalidDataException("Archiwum ZIP nie zawiera żadnego pliku CSV.");
            }

            foreach (ZipArchiveEntry entry in csvEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = Path.Combine(Path.GetTempPath(), "CSVForge", "zip", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                string extractedPath = Path.Combine(directory, "data.csv");
                try
                {
                    await using Stream source = entry.Open();
                    await using FileStream destination = new(extractedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    await source.CopyToAsync(destination, cancellationToken);
                    extractedFiles.Add(new ExtractedCsvFile(entry.FullName, extractedPath));
                }
                catch
                {
                    TryDeleteDirectory(directory);
                    throw;
                }
            }

            return extractedFiles;
        }
        catch
        {
            foreach (ExtractedCsvFile file in extractedFiles)
            {
                file.Dispose();
            }
            throw;
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; the next application startup retries orphan removal.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup; the next application startup retries orphan removal.
        }
    }

    public sealed class ExtractedCsvFile(string entryName, string filePath) : IDisposable
    {
        public string EntryName { get; } = entryName;
        public string FilePath { get; } = filePath;

        public void Dispose() => TryDeleteDirectory(Path.GetDirectoryName(FilePath)!);
    }
}
