using System.Text;
using System.Globalization;
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
        if (string.IsNullOrWhiteSpace(request.SourceSql))
        {
            SqliteIdentifierGuard.Table(request.TableName);
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
        IReadOnlyList<string> tableColumns = string.IsNullOrWhiteSpace(request.SourceSql)
            ? await ReadColumnsAsync(connection, request.TableName, cancellationToken)
            : request.Columns is { Count: > 0 }
                ? request.Columns
                : throw new ArgumentException("Columns are required when exporting an SQL result.", nameof(request));
        IReadOnlyList<string> columns = request.Columns is { Count: > 0 }
            ? request.Columns
            : tableColumns;
        HashSet<string> availableColumns = tableColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (columns.Any(column => !availableColumns.Contains(column)))
        {
            throw new ArgumentException("At least one selected export column does not exist.", nameof(request));
        }
        SqliteIdentifierGuard.Columns(columns);
        await using SqliteCommand command = connection.CreateCommand();
        string projection = string.Join(", ", columns.Select(CsvImportNameHelper.QuoteIdentifier));
        string source = string.IsNullOrWhiteSpace(request.SourceSql)
            ? CsvImportNameHelper.QuoteIdentifier(request.TableName)
            : $"({NormalizeSql(request.SourceSql)}) AS _sql_result";
        string whereClause = SqliteExportFilterBuilder.Build(command, tableColumns, request.ColumnFilters);
        command.CommandText = $"SELECT {projection} FROM {source}{whereClause};";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        await using StreamWriter writer = new(temporaryPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        string delimiter = request.Delimiter.ToString();

        if (request.IncludeHeader)
        {
            string header = string.Join(delimiter, Enumerable.Range(0, reader.FieldCount).Select(index => Escape(reader.GetName(index), request.Delimiter, false)));
            await writer.WriteLineAsync(header.AsMemory(), cancellationToken);
        }

        long exportedRows = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            string row = string.Join(delimiter, Enumerable.Range(0, reader.FieldCount)
                .Select(index => Escape(
                    reader.IsDBNull(index) ? string.Empty : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty,
                    request.Delimiter,
                    request.ProtectExcelFormulas)));
            await writer.WriteLineAsync(row.AsMemory(), cancellationToken);
            exportedRows++;
        }

        return exportedRows;
    }

    private static async Task<IReadOnlyList<string>> ReadColumnsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({CsvImportNameHelper.QuoteIdentifier(tableName)});";
        List<string> columns = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }
        if (columns.Count == 0)
        {
            throw new InvalidOperationException($"Table '{tableName}' does not exist or has no columns.");
        }
        return columns;
    }

    private static string Escape(string value, char delimiter, bool protectExcelFormulas)
    {
        if (protectExcelFormulas && value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
        {
            value = "'" + value;
        }
        if (!value.Contains(delimiter) && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string NormalizeSql(string sql)
    {
        string normalized = sql.Trim();
        while (normalized.EndsWith(';'))
        {
            normalized = normalized[..^1].TrimEnd();
        }
        if (normalized.Length == 0)
        {
            throw new ArgumentException("SQL query is required.", nameof(sql));
        }
        return normalized;
    }
}
