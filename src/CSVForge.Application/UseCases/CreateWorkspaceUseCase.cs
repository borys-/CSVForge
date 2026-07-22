using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Domain.Workspaces;

namespace CSVForge.Application.UseCases;

internal sealed class CreateWorkspaceUseCase(IWorkspaceService workspaceService) : ICreateWorkspaceUseCase
{
    public Task<Workspace> ExecuteAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        return workspaceService.CreateAsync(workspacePath, cancellationToken);
    }
}
