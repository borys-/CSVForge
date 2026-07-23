using CSVForge.Application.Sql;

namespace CSVForge.Application.Abstractions;

public interface IGetSqlSchemaUseCase
{
    Task<SqlSchemaSnapshot> ExecuteAsync(CancellationToken cancellationToken = default);
}
