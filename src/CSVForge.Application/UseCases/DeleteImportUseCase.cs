using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;

namespace CSVForge.Application.UseCases;

internal sealed class DeleteImportUseCase(IWorkspaceService workspaceService) : IDeleteImportUseCase
{
    public Task ExecuteAsync(Guid importId, CancellationToken cancellationToken = default) =>
        workspaceService.DeleteImportAsync(importId, cancellationToken);
}
