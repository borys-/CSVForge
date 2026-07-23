using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Application.Sql;

namespace CSVForge.Application.UseCases;

internal sealed class ExecuteSqlUseCase(ISqlExecutor sqlExecutor) : IExecuteSqlUseCase
{
    public Task<SqlQueryResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("Zapytanie SQL nie może być puste.", nameof(sql));
        }

        return sqlExecutor.ExecuteAsync(sql, cancellationToken);
    }
}
