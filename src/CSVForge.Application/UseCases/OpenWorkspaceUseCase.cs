using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Domain.Workspaces;

namespace CSVForge.Application.UseCases;

internal sealed class OpenWorkspaceUseCase(IWorkspaceService workspaceService) : IOpenWorkspaceUseCase
{
    public Task<Workspace> ExecuteAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        return workspaceService.OpenAsync(workspacePath, cancellationToken);
    }
}
