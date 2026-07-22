using CSVForge.Application.Abstractions;
using CSVForge.Application.Operations;
using CSVForge.Application.Ports;

namespace CSVForge.Application.UseCases;

internal sealed class ListOperationsUseCase(IOperationHistory history) : IListOperationsUseCase
{
    public Task<IReadOnlyList<WorkspaceOperation>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        history.ListAsync(cancellationToken);
}
