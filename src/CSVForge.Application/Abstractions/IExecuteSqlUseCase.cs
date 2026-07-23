using CSVForge.Application.Sql;

namespace CSVForge.Application.Abstractions;

public interface IExecuteSqlUseCase
{
    Task<SqlQueryResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default);
}
