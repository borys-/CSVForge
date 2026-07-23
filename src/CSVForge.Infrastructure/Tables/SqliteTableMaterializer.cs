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

        SqliteIdentifierGuard.Table(request.SourceTableName);
        SqliteIdentifierGuard.Table(request.TargetTableName);
        SqliteIdentifierGuard.Columns(request.Columns);

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);
        IReadOnlyList<string> available = await ReadColumnsAsync(connection, request.SourceTableName, cancellationToken);
        HashSet<string> availableSet = available.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (request.Columns.Any(column => !availableSet.Contains(column)))
        {
            throw new ArgumentException("Wybrana kolumna nie istnieje w tabeli źródłowej.", nameof(request));
        }

        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        string source = CsvImportNameHelper.QuoteIdentifier(request.SourceTableName);
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

        await using (SqliteCommand history = connection.CreateCommand())
        {
            history.Transaction = transaction;
            history.CommandText = """
                INSERT INTO _workspace_operations (id, operation_type, result_table_name, created_at, message)
                VALUES ($id, 'export_table', $table, $createdAt, $message);
                """;
            history.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            history.Parameters.AddWithValue("$table", request.TargetTableName);
            history.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
            history.Parameters.AddWithValue("$message", $"Utworzono tabelę '{request.TargetTableName}' ({rowCount} wierszy).");
            await history.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new CreateTableFromResultResult(request.TargetTableName, rowCount);
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
