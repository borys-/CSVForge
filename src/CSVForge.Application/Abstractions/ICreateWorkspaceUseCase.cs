using CSVForge.Domain.Workspaces;

namespace CSVForge.Application.Abstractions;

public interface ICreateWorkspaceUseCase
{
    Task<Workspace> ExecuteAsync(string workspacePath, CancellationToken cancellationToken = default);
}
