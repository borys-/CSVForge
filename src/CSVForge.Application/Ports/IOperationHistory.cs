using CSVForge.Application.Operations;

namespace CSVForge.Application.Ports;

public interface IOperationHistory
{
    Task<IReadOnlyList<WorkspaceOperation>> ListAsync(CancellationToken cancellationToken);
}
