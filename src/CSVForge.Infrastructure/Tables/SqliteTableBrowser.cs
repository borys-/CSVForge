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
        long totalRows = await CountRowsAsync(connection, request.TableName, cancellationToken);
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = await ReadRowsAsync(connection, request, columns, cancellationToken);

        return new TablePage(columns, rows, totalRows, request.Limit, request.Offset);
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

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {CsvImportNameHelper.QuoteIdentifier(tableName)};";

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
            {orderBy}
            LIMIT $limit OFFSET $offset;
            """;
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

}
