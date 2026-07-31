using CSVForge.Application.Ports;
using CSVForge.Domain.Operations;
using CSVForge.Infrastructure.Csv;
using CSVForge.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Operations;

internal sealed class SqliteDuplicateFinder(IWorkspaceContext workspaceContext) : IDuplicateFinder
{
    public async Task<OperationResult> FindAsync(DuplicateSearchRequest request, CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            throw new InvalidOperationException("Open or create a workspace before finding duplicates.");
        }

        if (request.KeyColumns.Count == 0)
        {
            throw new ArgumentException("At least one key column is required.", nameof(request));
        }
        SqliteIdentifierGuard.Table(request.TableName);
        SqliteIdentifierGuard.Columns(request.KeyColumns);

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);
        await SqliteIndexHelper.EnsureAsync(connection, request.TableName, request.KeyColumns, cancellationToken);

        string resultTableName = $"_duplicates_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        string keyColumnsSql = string.Join(", ", request.KeyColumns.Select(CsvImportNameHelper.QuoteIdentifier));
        string emptyFilter = BuildEmptyFilter(request);

        await using SqliteCommand command = connection.CreateCommand();
        string sql = request.Mode == DuplicateSearchMode.Summary
            ? BuildSummarySql(request.TableName, resultTableName, keyColumnsSql, emptyFilter)
            : BuildRowsSql(request.TableName, resultTableName, keyColumnsSql, emptyFilter);
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        long duplicateCount = await CountRowsAsync(connection, resultTableName, cancellationToken);
        await SaveOperationAsync(connection, "duplicates", resultTableName, $"Found {duplicateCount} duplicate result rows.", cancellationToken);

        return OperationResult.Ok(resultTableName, $"Znaleziono {duplicateCount} wierszy wyniku duplikatów.", sql);
    }

    private static string BuildSummarySql(string tableName, string resultTableName, string keyColumnsSql, string emptyFilter)
    {
        return $"""
            CREATE TABLE {CsvImportNameHelper.QuoteIdentifier(resultTableName)} AS
            SELECT {keyColumnsSql}, COUNT(*) AS duplicate_count
            FROM {CsvImportNameHelper.QuoteIdentifier(tableName)}
            {emptyFilter}
            GROUP BY {keyColumnsSql}
            HAVING COUNT(*) > 1;
            """;
    }

    private static string BuildRowsSql(string tableName, string resultTableName, string keyColumnsSql, string emptyFilter)
    {
        string sourceTable = CsvImportNameHelper.QuoteIdentifier(tableName);
        return $"""
            CREATE TABLE {CsvImportNameHelper.QuoteIdentifier(resultTableName)} AS
            SELECT source.*
            FROM {sourceTable} AS source
            INNER JOIN (
                SELECT {keyColumnsSql}
                FROM {sourceTable}
                {emptyFilter}
                GROUP BY {keyColumnsSql}
                HAVING COUNT(*) > 1
            ) AS duplicates
            ON {BuildJoinPredicate("source", "duplicates", keyColumnsSql)}
            ;
            """;
    }

    private static string BuildJoinPredicate(string leftAlias, string rightAlias, string keyColumnsSql)
    {
        return string.Join(" AND ", keyColumnsSql.Split(", ").Select(column => $"{leftAlias}.{column} = {rightAlias}.{column}"));
    }

    private static string BuildEmptyFilter(DuplicateSearchRequest request)
    {
        if (!request.IgnoreEmptyValues)
        {
            return string.Empty;
        }

        string predicate = string.Join(" AND ", request.KeyColumns.Select(column =>
            $"NULLIF(TRIM({CsvImportNameHelper.QuoteIdentifier(column)}), '') IS NOT NULL"));
        return $"WHERE {predicate}";
    }

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {CsvImportNameHelper.QuoteIdentifier(tableName)};";
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    private static async Task SaveOperationAsync(SqliteConnection connection, string operationType, string resultTableName, string message, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO _workspace_operations (id, operation_type, result_table_name, created_at, message)
            VALUES ($id, $operationType, $resultTableName, $createdAt, $message);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$operationType", operationType);
        command.Parameters.AddWithValue("$resultTableName", resultTableName);
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$message", message);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
