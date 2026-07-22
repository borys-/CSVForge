using System.Globalization;
using System.Text;
using CSVForge.Application.Ports;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure.Sqlite;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Csv;

internal sealed class CsvImporterService(IWorkspaceContext workspaceContext) : ICsvImporter
{
    private const int BatchSize = 500;

    public async Task<ImportResult> ImportAsync(ImportRequest request, IProgress<ImportProgress>? progress, CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            throw new InvalidOperationException("Open or create a workspace before importing CSV files.");
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding = string.IsNullOrWhiteSpace(request.EncodingName)
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            : Encoding.GetEncoding(request.EncodingName);

        char delimiter = request.Delimiter ?? await CsvImportNameHelper.DetectDelimiterAsync(request.FilePath, encoding, cancellationToken);

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);

        CsvImport import = await ImportRowsAsync(connection, request, encoding, delimiter, progress, cancellationToken);
        IReadOnlyList<ImportError> errors = await ReadErrorsAsync(connection, import.Id, cancellationToken);

        return new ImportResult(import, errors);
    }

    private static async Task<CsvImport> ImportRowsAsync(
        SqliteConnection connection,
        ImportRequest request,
        Encoding encoding,
        char delimiter,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(request.FilePath);
        using StreamReader reader = new(stream, encoding, detectEncodingFromByteOrderMarks: true);
        using CsvReader csv = new(reader, CreateConfiguration(delimiter));

        if (!await csv.ReadAsync())
        {
            throw new InvalidOperationException("CSV file is empty.");
        }

        string[] firstRecord = csv.Parser.Record ?? [];
        IReadOnlyList<string> originalHeaders = request.HasHeader
            ? firstRecord
            : CsvImportNameHelper.GenerateColumns(firstRecord.Length);
        IReadOnlyList<string> columnNames = request.HasHeader
            ? CsvImportNameHelper.NormalizeColumns(firstRecord)
            : originalHeaders;

        Guid importId = Guid.NewGuid();
        string tableName = CsvImportNameHelper.CreateTableName(request.DisplayName);
        await CreateImportTableAsync(connection, tableName, columnNames, cancellationToken);

        long rowCount = 0;
        List<string[]> batch = [];
        if (!request.HasHeader)
        {
            batch.Add(firstRecord);
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(csv.Parser.Record ?? []);

            if (batch.Count >= BatchSize)
            {
                rowCount += await InsertBatchAsync(connection, tableName, columnNames, batch, cancellationToken);
                batch.Clear();
                progress?.Report(new ImportProgress(rowCount, null, "Importing rows"));
            }
        }

        if (batch.Count > 0)
        {
            rowCount += await InsertBatchAsync(connection, tableName, columnNames, batch, cancellationToken);
        }

        DateTimeOffset importedAt = DateTimeOffset.UtcNow;
        CsvImport import = new(
            importId,
            request.DisplayName,
            request.FilePath,
            tableName,
            importedAt,
            rowCount,
            originalHeaders.Select((header, index) => new CsvColumn(header, columnNames[index], index)).ToArray());

        await SaveMetadataAsync(connection, import, cancellationToken);
        progress?.Report(new ImportProgress(rowCount, rowCount, "Import completed"));

        return import;
    }

    private static CsvConfiguration CreateConfiguration(char delimiter)
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter.ToString(),
            BadDataFound = null,
            MissingFieldFound = null
        };
    }

    private static async Task CreateImportTableAsync(SqliteConnection connection, string tableName, IReadOnlyList<string> columnNames, CancellationToken cancellationToken)
    {
        string columnsSql = string.Join(", ", columnNames.Select(column => $"{CsvImportNameHelper.QuoteIdentifier(column)} TEXT"));
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"CREATE TABLE {CsvImportNameHelper.QuoteIdentifier(tableName)} ({columnsSql});";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> InsertBatchAsync(SqliteConnection connection, string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<string[]> rows, CancellationToken cancellationToken)
    {
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (string[] row in rows)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = BuildInsertSql(tableName, columnNames);

            for (int i = 0; i < columnNames.Count; i++)
            {
                command.Parameters.AddWithValue($"$p{i}", i < row.Length ? row[i] : DBNull.Value);
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return rows.Count;
    }

    private static string BuildInsertSql(string tableName, IReadOnlyList<string> columnNames)
    {
        string columnsSql = string.Join(", ", columnNames.Select(CsvImportNameHelper.QuoteIdentifier));
        string parametersSql = string.Join(", ", columnNames.Select((_, index) => $"$p{index}"));
        return $"INSERT INTO {CsvImportNameHelper.QuoteIdentifier(tableName)} ({columnsSql}) VALUES ({parametersSql});";
    }

    private static async Task SaveMetadataAsync(SqliteConnection connection, CsvImport import, CancellationToken cancellationToken)
    {
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using SqliteCommand importCommand = connection.CreateCommand();
        importCommand.Transaction = transaction;
        importCommand.CommandText = """
            INSERT INTO _workspace_imports (id, display_name, source_path, table_name, imported_at, row_count)
            VALUES ($id, $displayName, $sourcePath, $tableName, $importedAt, $rowCount);
            """;
        importCommand.Parameters.AddWithValue("$id", import.Id.ToString());
        importCommand.Parameters.AddWithValue("$displayName", import.DisplayName);
        importCommand.Parameters.AddWithValue("$sourcePath", import.SourcePath);
        importCommand.Parameters.AddWithValue("$tableName", import.TableName);
        importCommand.Parameters.AddWithValue("$importedAt", import.ImportedAt.ToString("O"));
        importCommand.Parameters.AddWithValue("$rowCount", import.RowCount);
        await importCommand.ExecuteNonQueryAsync(cancellationToken);

        foreach (CsvColumn column in import.Columns)
        {
            await using SqliteCommand columnCommand = connection.CreateCommand();
            columnCommand.Transaction = transaction;
            columnCommand.CommandText = """
                INSERT INTO _workspace_columns (import_id, column_index, original_name, name)
                VALUES ($importId, $columnIndex, $originalName, $name);
                """;
            columnCommand.Parameters.AddWithValue("$importId", import.Id.ToString());
            columnCommand.Parameters.AddWithValue("$columnIndex", column.Index);
            columnCommand.Parameters.AddWithValue("$originalName", column.OriginalName);
            columnCommand.Parameters.AddWithValue("$name", column.Name);
            await columnCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<ImportError>> ReadErrorsAsync(SqliteConnection connection, Guid importId, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Array.Empty<ImportError>();
    }
}
