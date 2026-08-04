namespace CSVForge.Application.Ports;

public interface IWorkspaceMaintenanceService
{
    Task<string> PrepareOptimizedCopyAsync(string workspacePath, CancellationToken cancellationToken);
    Task<bool> TryReplaceWithOptimizedCopyAsync(string workspacePath, string optimizedCopyPath, CancellationToken cancellationToken);
    void DiscardOptimizedCopy(string optimizedCopyPath);
}
