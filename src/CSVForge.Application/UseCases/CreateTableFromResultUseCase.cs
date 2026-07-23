using CSVForge.Application.Abstractions;
using CSVForge.Application.Export;
using CSVForge.Application.Ports;

namespace CSVForge.Application.UseCases;

internal sealed class CreateTableFromResultUseCase(ITableMaterializer materializer)
    : ICreateTableFromResultUseCase
{
    public Task<CreateTableFromResultResult> ExecuteAsync(
        CreateTableFromResultRequest request,
        CancellationToken cancellationToken = default)
    {
        return materializer.CreateAsync(request, cancellationToken);
    }
}
