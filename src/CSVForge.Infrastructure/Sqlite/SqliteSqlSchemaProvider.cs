using CSVForge.Application.Ports;
using CSVForge.Application.Sql;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Sqlite;

internal sealed class SqliteSqlSchemaProvider(IWorkspaceContext workspaceContext) : ISqlSchemaProvider
{
    public async Task<SqlSchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            return SqlSchemaSnapshot.Empty;
        }

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT schema.name, schema.type, COALESCE(imports.display_name, schema.name)
            FROM sqlite_schema AS schema
            LEFT JOIN _workspace_imports AS imports ON imports.table_name = schema.name
            WHERE schema.type IN ('table', 'view')
              AND schema.name NOT LIKE 'sqlite_%'
              AND schema.name NOT LIKE '\_%' ESCAPE '\'
            ORDER BY schema.name;
            """;

        List<(string Name, SqlSchemaObjectKind Kind, string DisplayName)> objects = [];
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                objects.Add((
                    reader.GetString(0),
                    reader.GetString(1) == "view" ? SqlSchemaObjectKind.View : SqlSchemaObjectKind.Table,
                    reader.GetString(2)));
            }
        }

        List<SqlSchemaObject> schemaObjects = [];
        foreach ((string name, SqlSchemaObjectKind kind, string displayName) in objects)
        {
            await using SqliteCommand columnsCommand = connection.CreateCommand();
            columnsCommand.CommandText = "SELECT name FROM pragma_table_info($table) ORDER BY cid;";
            columnsCommand.Parameters.AddWithValue("$table", name);
            List<string> columns = [];
            await using SqliteDataReader reader = await columnsCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }
            schemaObjects.Add(new SqlSchemaObject(name, kind, columns, displayName));
        }

        return new SqlSchemaSnapshot(schemaObjects);
    }
}
