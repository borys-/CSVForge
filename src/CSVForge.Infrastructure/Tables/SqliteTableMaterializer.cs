using CSVForge.Application.Export;
using CSVForge.Application.Ports;
using CSVForge.Infrastructure.Csv;
using CSVForge.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Tables;

internal sealed class SqliteTableMaterializer(IWorkspaceContext workspaceContext) : ITableMaterializer
{
    public async Task<CreateTableFromResultResult> CreateAsync(
        CreateTableFromResultRequest request,
        CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            throw new InvalidOperationException("Otwórz workspace przed utworzeniem tabeli.");
        }
        if (request.Columns.Count == 0)
        {
            throw new ArgumentException("Wybierz co najmniej jedną kolumnę.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SourceSql))
        {
            SqliteIdentifierGuard.Table(request.SourceTableName);
        }
        SqliteIdentifierGuard.Table(request.TargetTableName);
        SqliteIdentifierGuard.Columns(request.Columns);

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);
        IReadOnlyList<string> available = string.IsNullOrWhiteSpace(request.SourceSql)
            ? await ReadColumnsAsync(connection, request.SourceTableName, cancellationToken)
            : request.Columns;
        HashSet<string> availableSet = available.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (request.Columns.Any(column => !availableSet.Contains(column)))
        {
            throw new ArgumentException("Wybrana kolumna nie istnieje w tabeli źródłowej.", nameof(request));
        }

        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        string source = string.IsNullOrWhiteSpace(request.SourceSql)
            ? CsvImportNameHelper.QuoteIdentifier(request.SourceTableName)
            : $"({NormalizeSql(request.SourceSql)}) AS _sql_result";
        string target = CsvImportNameHelper.QuoteIdentifier(request.TargetTableName);
        string projection = string.Join(", ", request.Columns.Select(CsvImportNameHelper.QuoteIdentifier));
        string where = string.IsNullOrWhiteSpace(request.TextFilter)
            ? string.Empty
            : " WHERE " + string.Join(" OR ", available.Select(column =>
                $"{CsvImportNameHelper.QuoteIdentifier(column)} LIKE $filter"));

        await using (SqliteCommand create = connection.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText = $"CREATE TABLE {target} AS SELECT {projection} FROM {source}{where};";
            if (!string.IsNullOrWhiteSpace(request.TextFilter))
            {
                create.Parameters.AddWithValue("$filter", $"%{request.TextFilter}%");
            }
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        long rowCount;
        await using (SqliteCommand count = connection.CreateCommand())
        {
            count.Transaction = transaction;
            count.CommandText = $"SELECT COUNT(*) FROM {target};";
            rowCount = (long)(await count.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        Guid importId = Guid.NewGuid();
        await using (SqliteCommand import = connection.CreateCommand())
        {
            import.Transaction = transaction;
            import.CommandText = """
                INSERT INTO _workspace_imports (id, display_name, source_path, table_name, imported_at, row_count)
                VALUES ($id, $displayName, $sourcePath, $tableName, $importedAt, $rowCount);
                """;
            import.Parameters.AddWithValue("$id", importId.ToString());
            import.Parameters.AddWithValue("$displayName", request.TargetTableName);
            import.Parameters.AddWithValue("$sourcePath", $"workspace://{request.SourceTableName}");
            import.Parameters.AddWithValue("$tableName", request.TargetTableName);
            import.Parameters.AddWithValue("$importedAt", DateTimeOffset.UtcNow.ToString("O"));
            import.Parameters.AddWithValue("$rowCount", rowCount);
            await import.ExecuteNonQueryAsync(cancellationToken);
        }

        for (int index = 0; index < request.Columns.Count; index++)
        {
            await using SqliteCommand column = connection.CreateCommand();
            column.Transaction = transaction;
            column.CommandText = """
                INSERT INTO _workspace_columns (import_id, column_index, original_name, name)
                VALUES ($importId, $columnIndex, $originalName, $name);
                """;
            column.Parameters.AddWithValue("$importId", importId.ToString());
            column.Parameters.AddWithValue("$columnIndex", index);
            column.Parameters.AddWithValue("$originalName", request.Columns[index]);
            column.Parameters.AddWithValue("$name", request.Columns[index]);
            await column.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new CreateTableFromResultResult(request.TargetTableName, rowCount);
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
            throw new ArgumentException("Zapytanie SQL jest wymagane.", nameof(sql));
        }
        return normalized;
    }

    private static async Task<IReadOnlyList<string>> ReadColumnsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Tabela źródłowa nie istnieje albo nie ma kolumn.");
        }
        return columns;
    }
}
