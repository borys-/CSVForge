using CSVForge.Application.Ports;
using CSVForge.Domain.Operations;
using CSVForge.Infrastructure.Csv;
using CSVForge.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Operations;

internal sealed class SqliteDatasetComparer(IWorkspaceContext workspaceContext) : IDatasetComparer
{
    private const string StatusColumn = "status_porównania";
    private const string CommonStatus = "wspólne";
    private const string LeftOnlyStatus = "tylko lewy";
    private const string RightOnlyStatus = "tylko prawy";

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
        SqliteIdentifierGuard.Table(request.LeftTableName);
        SqliteIdentifierGuard.Table(request.RightTableName);
        SqliteIdentifierGuard.Columns(request.LeftKeyColumns);
        SqliteIdentifierGuard.Columns(request.RightKeyColumns);

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);
        await SqliteIndexHelper.EnsureAsync(connection, request.LeftTableName, request.LeftKeyColumns, cancellationToken);
        await SqliteIndexHelper.EnsureAsync(connection, request.RightTableName, request.RightKeyColumns, cancellationToken);

        string resultTableName = $"compare_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = BuildSql(request, resultTableName);
        await command.ExecuteNonQueryAsync(cancellationToken);

        IReadOnlyDictionary<string, long> counts = await CountStatusesAsync(connection, resultTableName, cancellationToken);
        long rowCount = counts.Values.Sum();
        string details = string.Join(", ", counts.OrderBy(item => item.Key).Select(item => $"{item.Key}: {item.Value}"));
        string message = $"Porównanie zwróciło {rowCount} wierszy ({details}).";
        await SaveOperationAsync(connection, resultTableName, message, cancellationToken);
        return OperationResult.Ok(resultTableName, message);
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
                SELECT {leftKeys}, '{CommonStatus}' AS {CsvImportNameHelper.QuoteIdentifier(StatusColumn)}
                FROM {left} AS l
                INNER JOIN {right} AS r ON {joinPredicate};
                """,
            DatasetCompareMode.LeftOnly => $"""
                CREATE TABLE {result} AS
                SELECT {leftKeys}, '{LeftOnlyStatus}' AS {CsvImportNameHelper.QuoteIdentifier(StatusColumn)}
                FROM {left} AS l
                LEFT JOIN {right} AS r ON {joinPredicate}
                WHERE {BuildNullPredicate(request.RightKeyColumns, "r")};
                """,
            DatasetCompareMode.RightOnly => $"""
                CREATE TABLE {result} AS
                SELECT {rightKeys}, '{RightOnlyStatus}' AS {CsvImportNameHelper.QuoteIdentifier(StatusColumn)}
                FROM {right} AS r
                LEFT JOIN {left} AS l ON {joinPredicate}
                WHERE {BuildNullPredicate(request.LeftKeyColumns, "l")};
                """,
            DatasetCompareMode.DifferentRows => $"""
                CREATE TABLE {result} AS
                SELECT {leftKeys}, '{LeftOnlyStatus}' AS {CsvImportNameHelper.QuoteIdentifier(StatusColumn)}
                FROM {left} AS l
                LEFT JOIN {right} AS r ON {joinPredicate}
                WHERE {BuildNullPredicate(request.RightKeyColumns, "r")}
                UNION ALL
                SELECT {rightKeys}, '{RightOnlyStatus}' AS {CsvImportNameHelper.QuoteIdentifier(StatusColumn)}
                FROM {right} AS r
                LEFT JOIN {left} AS l ON {joinPredicate}
                WHERE {BuildNullPredicate(request.LeftKeyColumns, "l")};
                """,
            DatasetCompareMode.AllWithStatus => $"""
                CREATE TABLE {result} AS
                SELECT {leftKeys}, '{CommonStatus}' AS {CsvImportNameHelper.QuoteIdentifier(StatusColumn)}
                FROM {left} AS l
                INNER JOIN {right} AS r ON {joinPredicate}
                UNION ALL
                SELECT {leftKeys}, '{LeftOnlyStatus}' AS {CsvImportNameHelper.QuoteIdentifier(StatusColumn)}
                FROM {left} AS l
                LEFT JOIN {right} AS r ON {joinPredicate}
                WHERE {BuildNullPredicate(request.RightKeyColumns, "r")}
                UNION ALL
                SELECT {rightKeys}, '{RightOnlyStatus}' AS {CsvImportNameHelper.QuoteIdentifier(StatusColumn)}
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

    private static async Task<IReadOnlyDictionary<string, long>> CountStatusesAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        string statusColumn = CsvImportNameHelper.QuoteIdentifier(StatusColumn);
        command.CommandText = $"SELECT {statusColumn}, COUNT(*) FROM {CsvImportNameHelper.QuoteIdentifier(tableName)} GROUP BY {statusColumn};";
        Dictionary<string, long> counts = new(StringComparer.OrdinalIgnoreCase);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            counts[reader.GetString(0)] = reader.GetInt64(1);
        }
        return counts;
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
