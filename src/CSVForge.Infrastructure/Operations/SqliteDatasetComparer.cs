using CSVForge.Application.Ports;
using CSVForge.Domain.Operations;
using CSVForge.Infrastructure.Csv;
using CSVForge.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Operations;

internal sealed class SqliteDatasetComparer(IWorkspaceContext workspaceContext) : IDatasetComparer
{
    public async Task<OperationResult> CompareAsync(DatasetCompareRequest request, CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            throw new InvalidOperationException("Open or create a workspace before comparing datasets.");
        }

        if (request.LeftKeyColumns.Count == 0 || request.LeftKeyColumns.Count != request.RightKeyColumns.Count)
        {
            throw new ArgumentException("Compare keys must be non-empty and have the same number of columns.", nameof(request));
        }

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);
        await SqliteIndexHelper.EnsureAsync(connection, request.LeftTableName, request.LeftKeyColumns, cancellationToken);
        await SqliteIndexHelper.EnsureAsync(connection, request.RightTableName, request.RightKeyColumns, cancellationToken);

        string resultTableName = $"compare_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = BuildSql(request, resultTableName);
        await command.ExecuteNonQueryAsync(cancellationToken);

        long rowCount = await CountRowsAsync(connection, resultTableName, cancellationToken);
        await SaveOperationAsync(connection, resultTableName, $"Compare produced {rowCount} rows.", cancellationToken);

        return OperationResult.Ok(resultTableName, $"Porównanie zwróciło {rowCount} wierszy.");
    }

    private static string BuildSql(DatasetCompareRequest request, string resultTableName)
    {
        string left = CsvImportNameHelper.QuoteIdentifier(request.LeftTableName);
        string right = CsvImportNameHelper.QuoteIdentifier(request.RightTableName);
        string result = CsvImportNameHelper.QuoteIdentifier(resultTableName);
        string joinPredicate = BuildJoinPredicate(request, "l", "r");
        string leftKeys = BuildKeyProjection(request.LeftKeyColumns, "l");
        string rightKeys = BuildKeyProjection(request.RightKeyColumns, "r");

        return request.Mode switch
        {
            DatasetCompareMode.CommonRows => $"""
                CREATE TABLE {result} AS
                SELECT {leftKeys}, 'common' AS compare_status
                FROM {left} AS l
                INNER JOIN {right} AS r ON {joinPredicate};
                """,
            DatasetCompareMode.LeftOnly => $"""
                CREATE TABLE {result} AS
                SELECT {leftKeys}, 'left_only' AS compare_status
                FROM {left} AS l
                LEFT JOIN {right} AS r ON {joinPredicate}
                WHERE {BuildNullPredicate(request.RightKeyColumns, "r")};
                """,
            DatasetCompareMode.RightOnly => $"""
                CREATE TABLE {result} AS
                SELECT {rightKeys}, 'right_only' AS compare_status
                FROM {right} AS r
                LEFT JOIN {left} AS l ON {joinPredicate}
                WHERE {BuildNullPredicate(request.LeftKeyColumns, "l")};
                """,
            DatasetCompareMode.AllWithStatus => $"""
                CREATE TABLE {result} AS
                SELECT {leftKeys}, 'common' AS compare_status
                FROM {left} AS l
                INNER JOIN {right} AS r ON {joinPredicate}
                UNION ALL
                SELECT {leftKeys}, 'left_only' AS compare_status
                FROM {left} AS l
                LEFT JOIN {right} AS r ON {joinPredicate}
                WHERE {BuildNullPredicate(request.RightKeyColumns, "r")}
                UNION ALL
                SELECT {rightKeys}, 'right_only' AS compare_status
                FROM {right} AS r
                LEFT JOIN {left} AS l ON {joinPredicate}
                WHERE {BuildNullPredicate(request.LeftKeyColumns, "l")};
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Mode, "Unsupported compare mode.")
        };
    }

    private static string BuildJoinPredicate(DatasetCompareRequest request, string leftAlias, string rightAlias)
    {
        return string.Join(" AND ", request.LeftKeyColumns.Zip(request.RightKeyColumns, (leftColumn, rightColumn) =>
            $"{leftAlias}.{CsvImportNameHelper.QuoteIdentifier(leftColumn)} = {rightAlias}.{CsvImportNameHelper.QuoteIdentifier(rightColumn)}"));
    }

    private static string BuildKeyProjection(IReadOnlyList<string> columns, string alias)
    {
        return string.Join(", ", columns.Select(column => $"{alias}.{CsvImportNameHelper.QuoteIdentifier(column)} AS {CsvImportNameHelper.QuoteIdentifier(column)}"));
    }

    private static string BuildNullPredicate(IReadOnlyList<string> columns, string alias)
    {
        return string.Join(" AND ", columns.Select(column => $"{alias}.{CsvImportNameHelper.QuoteIdentifier(column)} IS NULL"));
    }

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {CsvImportNameHelper.QuoteIdentifier(tableName)};";
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    private static async Task SaveOperationAsync(SqliteConnection connection, string resultTableName, string message, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO _workspace_operations (id, operation_type, result_table_name, created_at, message)
            VALUES ($id, 'compare', $resultTableName, $createdAt, $message);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$resultTableName", resultTableName);
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$message", message);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
