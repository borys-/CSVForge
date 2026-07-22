using CSVForge.Application.Operations;

namespace CSVForge.Application.Abstractions;

public interface IListOperationsUseCase
{
    Task<IReadOnlyList<WorkspaceOperation>> ExecuteAsync(CancellationToken cancellationToken = default);
}
