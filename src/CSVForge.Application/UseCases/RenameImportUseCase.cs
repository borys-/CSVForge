using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;

namespace CSVForge.Application.UseCases;

internal sealed class RenameImportUseCase(IWorkspaceService workspaceService) : IRenameImportUseCase
{
    public Task ExecuteAsync(Guid importId, string displayName, CancellationToken cancellationToken = default) =>
        workspaceService.RenameImportAsync(importId, displayName, cancellationToken);
}
