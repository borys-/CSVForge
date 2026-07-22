using System.Text;
using CSVForge.Application.Export;
using CSVForge.Application.Ports;
using CSVForge.Infrastructure.Csv;
using CSVForge.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Tables;

internal sealed class SqliteTableExporter(IWorkspaceContext workspaceContext) : ITableExporter
{
    public async Task<ExportResult> ExportAsync(ExportTableRequest request, CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            throw new InvalidOperationException("Open or create a workspace before exporting a table.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(request));
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(request.OutputPath));
        if (directory is null || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Output directory '{directory}' does not exist.");
        }

        string outputPath = Path.GetFullPath(request.OutputPath);
        string temporaryPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            long exportedRows = await WriteTemporaryFileAsync(workspaceContext.CurrentWorkspacePath, request, temporaryPath, cancellationToken);
            File.Move(temporaryPath, outputPath, overwrite: true);
            return new ExportResult(outputPath, exportedRows);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    private static async Task<long> WriteTemporaryFileAsync(string workspacePath, ExportTableRequest request, string temporaryPath, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspacePath);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {CsvImportNameHelper.QuoteIdentifier(request.TableName)};";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        await using StreamWriter writer = new(temporaryPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        string delimiter = request.Delimiter.ToString();

        if (request.IncludeHeader)
        {
            string header = string.Join(delimiter, Enumerable.Range(0, reader.FieldCount).Select(index => Escape(reader.GetName(index), request.Delimiter)));
            await writer.WriteLineAsync(header.AsMemory(), cancellationToken);
        }

        long exportedRows = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            string row = string.Join(delimiter, Enumerable.Range(0, reader.FieldCount)
                .Select(index => Escape(reader.IsDBNull(index) ? string.Empty : reader.GetValue(index).ToString() ?? string.Empty, request.Delimiter)));
            await writer.WriteLineAsync(row.AsMemory(), cancellationToken);
            exportedRows++;
        }

        return exportedRows;
    }

    private static string Escape(string value, char delimiter)
    {
        if (!value.Contains(delimiter) && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
