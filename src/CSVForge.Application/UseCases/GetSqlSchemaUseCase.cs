using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Application.Sql;

namespace CSVForge.Application.UseCases;

internal sealed class GetSqlSchemaUseCase(ISqlSchemaProvider schemaProvider) : IGetSqlSchemaUseCase
{
    public Task<SqlSchemaSnapshot> ExecuteAsync(CancellationToken cancellationToken = default) =>
        schemaProvider.GetSchemaAsync(cancellationToken);
}
