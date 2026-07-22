using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Domain.Imports;

namespace CSVForge.Application.UseCases;

internal sealed class ListImportedTablesUseCase(IWorkspaceService workspaceService) : IListImportedTablesUseCase
{
    public Task<IReadOnlyList<CsvImport>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return workspaceService.ListImportsAsync(cancellationToken);
    }
}
