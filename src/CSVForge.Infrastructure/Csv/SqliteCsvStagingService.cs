using CSVForge.Application.Ports;
using CSVForge.Application.Csv;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure.Workspaces;

namespace CSVForge.Infrastructure.Csv;

internal sealed class SqliteCsvStagingService : ICsvStagingService
{
    public async Task<CsvStagingResult> StageAsync(ImportRequest request, CancellationToken cancellationToken)
    {
        string stagingDirectory = Path.Combine(Path.GetTempPath(), "CSVForge", "staging");
        Directory.CreateDirectory(stagingDirectory);
        string databasePath = Path.Combine(stagingDirectory, $"{Guid.NewGuid():N}.db");
        WorkspaceContext context = new();
        SqliteWorkspaceService workspace = new(context);
        await workspace.CreateAsync(databasePath, cancellationToken);
        try
        {
            CsvReaderService reader = new();
            CsvPreview preview = await reader.PreviewAsync(request, 100, cancellationToken);
            CsvImporterService importer = new(context);
            ImportResult imported = await importer.ImportAsync(request, null, cancellationToken);
            return new CsvStagingResult(databasePath, imported.Import.TableName, preview, imported.Import.RowCount);
        }
        catch
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Delete(databasePath);
                File.Delete(databasePath + "-wal");
                File.Delete(databasePath + "-shm");
            }
            catch (IOException) { }
            throw;
        }
    }
}
