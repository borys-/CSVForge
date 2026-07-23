using CSVForge.Application.Sql;

namespace CSVForge.Application.Ports;

public interface ISqlExecutor
{
    Task<SqlQueryResult> ExecuteAsync(string sql, CancellationToken cancellationToken);
}
