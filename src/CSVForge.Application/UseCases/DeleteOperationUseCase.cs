using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;

namespace CSVForge.Application.UseCases;

internal sealed class DeleteOperationUseCase(IOperationHistory history) : IDeleteOperationUseCase
{
    public Task ExecuteAsync(Guid operationId, CancellationToken cancellationToken = default) =>
        history.DeleteAsync(operationId, cancellationToken);
}
