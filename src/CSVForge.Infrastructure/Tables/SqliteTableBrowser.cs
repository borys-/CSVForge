using CSVForge.Application.Ports;
using CSVForge.Application.Tables;
using CSVForge.Infrastructure.Csv;
using CSVForge.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace CSVForge.Infrastructure.Tables;

internal sealed class SqliteTableBrowser(IWorkspaceContext workspaceContext) : ITableBrowser
{
    public async Task<TablePage> BrowseAsync(BrowseTableRequest request, CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            throw new InvalidOperationException("Open or create a workspace before browsing tables.");
        }
        SqliteIdentifierGuard.Table(request.TableName);

        if (request.Limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Limit must be greater than zero.");
        }

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);

        IReadOnlyList<string> columns = await ReadColumnNamesAsync(connection, request.TableName, cancellationToken);
        long totalRows = await CountRowsAsync(connection, request, columns, cancellationToken);
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = await ReadRowsAsync(connection, request, columns, cancellationToken);

        return new TablePage(columns, rows, totalRows, request.Limit, request.Offset);
    }

    public async Task<IReadOnlyList<ColumnValueOption>> GetColumnValuesAsync(
        ColumnValuesRequest request,
        CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            throw new InvalidOperationException("Otwórz workspace przed filtrowaniem tabel.");
        }
        SqliteIdentifierGuard.Table(request.TableName);
        SqliteIdentifierGuard.Columns([request.ColumnName]);
        if (request.Limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Limit musi być większy od zera.");
        }

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);
        IReadOnlyList<string> columns = await ReadColumnNamesAsync(connection, request.TableName, cancellationToken);
        if (!columns.Contains(request.ColumnName, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Kolumna '{request.ColumnName}' nie istnieje.", nameof(request));
        }

        Dictionary<string, IReadOnlyList<string?>> otherFilters = (request.ColumnFilters ?? new Dictionary<string, IReadOnlyList<string?>>())
            .Where(filter => !string.Equals(filter.Key, request.ColumnName, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(filter => filter.Key, filter => filter.Value, StringComparer.OrdinalIgnoreCase);
        BrowseTableRequest browseRequest = new(
            request.TableName, request.Limit, 0, null, false, request.TextFilter, otherFilters);
        await using SqliteCommand command = connection.CreateCommand();
        string column = CsvImportNameHelper.QuoteIdentifier(request.ColumnName);
        command.CommandText = $"""
            SELECT {column}, COUNT(*)
            FROM {CsvImportNameHelper.QuoteIdentifier(request.TableName)}
            {BuildWhereClause(browseRequest, columns)}
            GROUP BY {column}
            ORDER BY {column}
            LIMIT $valuesLimit;
            """;
        AddFilterParameters(command, browseRequest);
        command.Parameters.AddWithValue("$valuesLimit", request.Limit);

        List<ColumnValueOption> values = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new ColumnValueOption(
                reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.GetInt64(1)));
        }
        return values;
    }

    private static async Task<IReadOnlyList<string>> ReadColumnNamesAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
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

    private static async Task<long> CountRowsAsync(SqliteConnection connection, BrowseTableRequest request, IReadOnlyList<string> columns, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {CsvImportNameHelper.QuoteIdentifier(request.TableName)}{BuildWhereClause(request, columns)};";
        AddFilterParameters(command, request);

        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> ReadRowsAsync(
        SqliteConnection connection,
        BrowseTableRequest request,
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken)
    {
        string selectColumns = string.Join(", ", columns.Select(CsvImportNameHelper.QuoteIdentifier));
        string orderBy = BuildOrderByClause(request, columns);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {selectColumns}
            FROM {CsvImportNameHelper.QuoteIdentifier(request.TableName)}
            {BuildWhereClause(request, columns)}
            {orderBy}
            LIMIT $limit OFFSET $offset;
            """;
        AddFilterParameters(command, request);
        command.Parameters.AddWithValue("$limit", request.Limit);
        command.Parameters.AddWithValue("$offset", Math.Max(0, request.Offset));

        List<IReadOnlyDictionary<string, string?>> rows = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            Dictionary<string, string?> row = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < columns.Count; i++)
            {
                row[columns[i]] = reader.IsDBNull(i)
                    ? null
                    : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string BuildWhereClause(BrowseTableRequest request, IReadOnlyList<string> columns)
    {
        List<string> predicates = [];
        if (!string.IsNullOrWhiteSpace(request.TextFilter))
        {
            predicates.Add("(" + string.Join(" OR ", columns.Select(column =>
                $"{CsvImportNameHelper.QuoteIdentifier(column)} LIKE $filter")) + ")");
        }

        int filterIndex = 0;
        foreach ((string column, IReadOnlyList<string?> values) in request.ColumnFilters ?? new Dictionary<string, IReadOnlyList<string?>>())
        {
            if (!columns.Contains(column, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Filter column '{column}' does not exist.", nameof(request));
            }
            if (values.Count == 0)
            {
                predicates.Add("0 = 1");
                filterIndex++;
                continue;
            }

            List<string> valuePredicates = [];
            int valueIndex = 0;
            foreach (string? value in values)
            {
                valuePredicates.Add(value is null
                    ? $"{CsvImportNameHelper.QuoteIdentifier(column)} IS NULL"
                    : $"{CsvImportNameHelper.QuoteIdentifier(column)} = $columnFilter{filterIndex}_{valueIndex++}");
            }
            predicates.Add("(" + string.Join(" OR ", valuePredicates) + ")");
            filterIndex++;
        }
        return predicates.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", predicates);
    }

    private static string BuildOrderByClause(BrowseTableRequest request, IReadOnlyList<string> columns)
    {
        if (string.IsNullOrWhiteSpace(request.SortColumn))
        {
            return string.Empty;
        }

        if (!columns.Contains(request.SortColumn, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Sort column '{request.SortColumn}' does not exist.", nameof(request));
        }

        string direction = request.SortDescending ? "DESC" : "ASC";
        return $" ORDER BY {CsvImportNameHelper.QuoteIdentifier(request.SortColumn)} {direction}";
    }

    private static void AddFilterParameters(SqliteCommand command, BrowseTableRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TextFilter))
        {
            command.Parameters.AddWithValue("$filter", $"%{request.TextFilter}%");
        }

        int filterIndex = 0;
        foreach (IReadOnlyList<string?> values in (request.ColumnFilters ?? new Dictionary<string, IReadOnlyList<string?>>()).Values)
        {
            int valueIndex = 0;
            foreach (string? value in values)
            {
                if (value is not null)
                {
                    command.Parameters.AddWithValue($"$columnFilter{filterIndex}_{valueIndex++}", value);
                }
            }
            filterIndex++;
        }
    }
}
