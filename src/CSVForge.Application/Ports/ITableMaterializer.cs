using CSVForge.Application.Export;

namespace CSVForge.Application.Ports;

public interface ITableMaterializer
{
    Task<CreateTableFromResultResult> CreateAsync(
        CreateTableFromResultRequest request,
        CancellationToken cancellationToken);
}
