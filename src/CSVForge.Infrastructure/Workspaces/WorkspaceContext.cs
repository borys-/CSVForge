using CSVForge.Application.Ports;

namespace CSVForge.Infrastructure.Workspaces;

internal sealed class WorkspaceContext : IWorkspaceContext
{
    public string? CurrentWorkspacePath { get; private set; }

    public void SetCurrentWorkspace(string workspacePath)
    {
        CurrentWorkspacePath = workspacePath;
    }
}
