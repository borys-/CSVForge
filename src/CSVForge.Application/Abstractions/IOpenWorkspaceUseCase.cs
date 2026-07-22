using CSVForge.Domain.Workspaces;

namespace CSVForge.Application.Abstractions;

public interface IOpenWorkspaceUseCase
{
    Task<Workspace> ExecuteAsync(string workspacePath, CancellationToken cancellationToken = default);
}
