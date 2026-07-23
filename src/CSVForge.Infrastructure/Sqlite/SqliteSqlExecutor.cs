using System.Globalization;
using CSVForge.Application.Ports;
using CSVForge.Application.Sql;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Sqlite;

internal sealed class SqliteSqlExecutor(IWorkspaceContext workspaceContext) : ISqlExecutor
{
    private const int MaxResultRows = 10_000;

    public async Task<SqlQueryResult> ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            throw new InvalidOperationException("Otwórz lub utwórz workspace przed wykonaniem SQL.");
        }

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<string> columns = [];
        List<IReadOnlyDictionary<string, string?>> rows = [];
        bool truncated = false;

        do
        {
            if (reader.FieldCount == 0)
            {
                continue;
            }

            columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            rows = [];
            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count >= MaxResultRows)
                {
                    truncated = true;
                    break;
                }

                Dictionary<string, string?> row = new(StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < columns.Count; index++)
                {
                    row[columns[index]] = reader.IsDBNull(index)
                        ? null
                        : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture);
                }
                rows.Add(row);
            }
        }
        while (!truncated && await reader.NextResultAsync(cancellationToken));

        return new SqlQueryResult(columns, rows, reader.RecordsAffected, truncated);
    }
}
