using CSVForge.Application.Ports;
using CSVForge.Domain.Operations;
using CSVForge.Infrastructure.Csv;
using CSVForge.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Operations;

internal sealed class SqliteDatasetJoiner(IWorkspaceContext workspaceContext) : IDatasetJoiner
{
    public async Task<OperationResult> JoinAsync(DatasetJoinRequest request, CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            throw new InvalidOperationException("Open or create a workspace before joining datasets.");
        }

        Validate(request);
        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);
        await SqliteIndexHelper.EnsureAsync(connection, request.LeftTableName, request.LeftJoinColumns, cancellationToken);
        await SqliteIndexHelper.EnsureAsync(connection, request.RightTableName, request.RightJoinColumns, cancellationToken);

        string resultTableName = $"join_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = BuildSql(request, resultTableName);
        await command.ExecuteNonQueryAsync(cancellationToken);

        long rowCount = await CountRowsAsync(connection, resultTableName, cancellationToken);
        await SaveOperationAsync(connection, resultTableName, request.JoinType, rowCount, cancellationToken);
        return OperationResult.Ok(resultTableName, $"Połączenie zwróciło {rowCount} wierszy.");
    }

    private static void Validate(DatasetJoinRequest request)
    {
        if (request.LeftJoinColumns.Count == 0 || request.LeftJoinColumns.Count != request.RightJoinColumns.Count)
        {
            throw new ArgumentException("Join keys must be non-empty and have the same number of columns.", nameof(request));
        }

        if (request.LeftOutputColumns.Count == 0 && request.RightOutputColumns.Count == 0)
        {
            throw new ArgumentException("At least one output column must be selected.", nameof(request));
        }
    }

    private static string BuildSql(DatasetJoinRequest request, string resultTableName)
    {
        string left = CsvImportNameHelper.QuoteIdentifier(request.LeftTableName);
        string right = CsvImportNameHelper.QuoteIdentifier(request.RightTableName);
        string result = CsvImportNameHelper.QuoteIdentifier(resultTableName);
        string projection = BuildProjection(request);
        string predicate = string.Join(" AND ", request.LeftJoinColumns.Zip(request.RightJoinColumns, (leftColumn, rightColumn) =>
            $"l.{CsvImportNameHelper.QuoteIdentifier(leftColumn)} = r.{CsvImportNameHelper.QuoteIdentifier(rightColumn)}"));

        string fromClause = request.JoinType switch
        {
            DatasetJoinType.Inner => $"{left} AS l INNER JOIN {right} AS r ON {predicate}",
            DatasetJoinType.Left => $"{left} AS l LEFT JOIN {right} AS r ON {predicate}",
            DatasetJoinType.Right => $"{right} AS r LEFT JOIN {left} AS l ON {predicate}",
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.JoinType, "Unsupported join type.")
        };

        return $"CREATE TABLE {result} AS SELECT {projection} FROM {fromClause};";
    }

    private static string BuildProjection(DatasetJoinRequest request)
    {
        HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);
        List<string> projections = [];
        AddColumns(request.LeftOutputColumns, "l", "left", usedNames, projections);
        AddColumns(request.RightOutputColumns, "r", "right", usedNames, projections);
        return string.Join(", ", projections);
    }

    private static void AddColumns(IReadOnlyList<string> columns, string tableAlias, string conflictPrefix, ISet<string> usedNames, ICollection<string> projections)
    {
        foreach (string column in columns)
        {
            string outputName = column;
            if (!usedNames.Add(outputName))
            {
                outputName = $"{conflictPrefix}_{column}";
                int suffix = 2;
                while (!usedNames.Add(outputName))
                {
                    outputName = $"{conflictPrefix}_{column}_{suffix++}";
                }
            }

            projections.Add($"{tableAlias}.{CsvImportNameHelper.QuoteIdentifier(column)} AS {CsvImportNameHelper.QuoteIdentifier(outputName)}");
        }
    }

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {CsvImportNameHelper.QuoteIdentifier(tableName)};";
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    private static async Task SaveOperationAsync(SqliteConnection connection, string resultTableName, DatasetJoinType joinType, long rowCount, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO _workspace_operations (id, operation_type, result_table_name, created_at, message)
            VALUES ($id, 'join', $resultTableName, $createdAt, $message);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$resultTableName", resultTableName);
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$message", $"{joinType} join produced {rowCount} rows.");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
