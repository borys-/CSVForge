using CSVForge.Application.Operations;
using CSVForge.Application.Ports;
using CSVForge.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Operations;

internal sealed class SqliteOperationHistory(IWorkspaceContext workspaceContext) : IOperationHistory
{
    public async Task<IReadOnlyList<WorkspaceOperation>> ListAsync(CancellationToken cancellationToken)
    {
        if (workspaceContext.CurrentWorkspacePath is null)
        {
            return [];
        }

        await using SqliteConnection connection = SqliteConnectionFactory.Create(workspaceContext.CurrentWorkspacePath);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, operation_type, result_table_name, created_at, message
            FROM _workspace_operations
            ORDER BY created_at DESC
            LIMIT 100;
            """;

        List<WorkspaceOperation> operations = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            operations.Add(new WorkspaceOperation(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3)),
                reader.GetString(4)));
        }
        return operations;
    }
}
