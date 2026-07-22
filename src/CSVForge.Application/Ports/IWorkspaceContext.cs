namespace CSVForge.Application.Ports;

public interface IWorkspaceContext
{
    string? CurrentWorkspacePath { get; }
    void SetCurrentWorkspace(string workspacePath);
}
