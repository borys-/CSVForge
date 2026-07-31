using CSVForge.Application.Ports;
using CSVForge.Domain.Operations;
using CSVForge.Infrastructure.Csv;
using CSVForge.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Operations;

internal sealed class SqliteDatasetComparer(IWorkspaceContext workspaceContext) : IDatasetComparer
{
    private const string StatusColumn = "status_porównania";

    public async Task<OperationResult> CompareAsync(DatasetCompareRequest request, CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            throw new InvalidOperationException("Otwórz workspace przed porównaniem danych.");
        }
        Validate(request);

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);
        foreach (DatasetCompareSource source in request.Sources)
        {
            SqliteIdentifierGuard.Table(source.TableName);
            SqliteIdentifierGuard.Columns(source.KeyColumns);
            await SqliteIndexHelper.EnsureAsync(connection, source.TableName, source.KeyColumns, cancellationToken);
        }

        string resultTableName = $"_compare_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        await using SqliteCommand command = connection.CreateCommand();
        string sql = BuildSql(request, resultTableName);
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        IReadOnlyDictionary<string, long> counts = await CountStatusesAsync(connection, resultTableName, cancellationToken);
        long rowCount = counts.Values.Sum();
        string details = string.Join(", ", counts.OrderBy(item => item.Key).Select(item => $"{item.Key}: {item.Value}"));
        string message = $"Porównanie {request.Sources.Count} plików zwróciło {rowCount} wierszy ({details}).";
        await SaveOperationAsync(connection, resultTableName, message, cancellationToken);
        return OperationResult.Ok(resultTableName, message, sql);
    }

    private static void Validate(DatasetCompareRequest request)
    {
        if (request.Sources.Count < 2)
        {
            throw new ArgumentException("Do porównania potrzebne są co najmniej dwa pliki.", nameof(request));
        }
        if (request.Sources.Select(source => source.TableName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Sources.Count)
        {
            throw new ArgumentException("Każdy plik można dodać do porównania tylko raz.", nameof(request));
        }
        int keyCount = request.Sources[0].KeyColumns.Count;
        if (keyCount == 0 || request.Sources.Any(source => source.KeyColumns.Count != keyCount))
        {
            throw new ArgumentException("Każdy plik musi mieć taką samą, niepustą liczbę kluczy.", nameof(request));
        }
    }

    private static string BuildSql(DatasetCompareRequest request, string resultTableName)
    {
        IReadOnlyList<string> outputColumns = request.Sources[0].KeyColumns;
        string union = string.Join("\nUNION\n", request.Sources.Select(source =>
            $"SELECT {BuildKeyProjection(source.KeyColumns, outputColumns)} FROM {Quote(source.TableName)}"));
        string[] presence = request.Sources.Select((source, index) =>
            $"EXISTS (SELECT 1 FROM {Quote(source.TableName)} AS s{index} WHERE {BuildMatch(outputColumns, source.KeyColumns, "k", $"s{index}")})").ToArray();
        string presenceCount = string.Join(" + ", presence.Select(value => $"CASE WHEN {value} THEN 1 ELSE 0 END"));
        string names = string.Join(" || ", presence.Select((value, index) =>
            $"CASE WHEN {value} THEN {SqlLiteral($"plik {index + 1}, ")} ELSE '' END"));
        string listedNames = $"rtrim(({names}), ', ')";
        string status = $"CASE WHEN ({presenceCount}) = 1 THEN 'Tylko w: ' || {listedNames} WHEN ({presenceCount}) = {request.Sources.Count} THEN 'We wszystkich plikach' ELSE 'W plikach: ' || {listedNames} END";
        string presenceColumns = string.Join(",\n       ", presence.Select((value, index) =>
            $"CASE WHEN {value} THEN '✓' ELSE '' END AS {Quote($"plik{index + 1}")}"));
        string filter = request.Mode switch
        {
            DatasetCompareMode.CommonRows => $"WHERE ({presenceCount}) = {request.Sources.Count}",
            DatasetCompareMode.LeftOnly => $"WHERE ({presenceCount}) = 1 AND {presence[0]}",
            DatasetCompareMode.RightOnly => $"WHERE ({presenceCount}) = 1 AND {presence[1]}",
            DatasetCompareMode.DifferentRows => $"WHERE ({presenceCount}) < {request.Sources.Count}",
            DatasetCompareMode.AllWithStatus => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Mode, "Nieobsługiwany tryb porównania.")
        };

        return $"""
            CREATE TABLE {Quote(resultTableName)} AS
            WITH all_keys AS (
                {union}
            )
            SELECT {string.Join(", ", outputColumns.Select(column => $"k.{Quote(column)}"))},
                   {presenceColumns},
                   {status} AS {Quote(StatusColumn)}
            FROM all_keys AS k
            {filter};
            """;
    }

    private static string BuildKeyProjection(IReadOnlyList<string> source, IReadOnlyList<string> output) =>
        string.Join(", ", source.Zip(output, (sourceColumn, outputColumn) =>
            $"{Quote(sourceColumn)} AS {Quote(outputColumn)}"));

    private static string BuildMatch(
        IReadOnlyList<string> output,
        IReadOnlyList<string> source,
        string outputAlias,
        string sourceAlias) =>
        string.Join(" AND ", output.Zip(source, (outputColumn, sourceColumn) =>
            $"{sourceAlias}.{Quote(sourceColumn)} = {outputAlias}.{Quote(outputColumn)}"));

    private static string Quote(string value) => CsvImportNameHelper.QuoteIdentifier(value);

    private static string SqlLiteral(string value) => $"'{value.Replace("'", "''")}'";

    private static async Task<IReadOnlyDictionary<string, long>> CountStatusesAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {Quote(StatusColumn)}, COUNT(*) FROM {Quote(tableName)} GROUP BY {Quote(StatusColumn)};";
        Dictionary<string, long> counts = new(StringComparer.OrdinalIgnoreCase);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            counts[reader.GetString(0)] = reader.GetInt64(1);
        }
        return counts;
    }

    private static async Task SaveOperationAsync(
        SqliteConnection connection,
        string resultTableName,
        string message,
        CancellationToken cancellationToken)
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
