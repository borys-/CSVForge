using CSVForge.Application.Sql;

namespace CSVForge.Application.Ports;

public interface ISqlSchemaProvider
{
    Task<SqlSchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken);
}
