using CSVForge.Domain.Imports;
using CSVForge.Domain.Workspaces;

namespace CSVForge.Application.Ports;

public interface IWorkspaceService
{
    Task<Workspace> CreateAsync(string workspacePath, CancellationToken cancellationToken);
    Task<Workspace> OpenAsync(string workspacePath, CancellationToken cancellationToken);
    Task<IReadOnlyList<CsvImport>> ListImportsAsync(CancellationToken cancellationToken);
    Task DeleteImportAsync(Guid importId, CancellationToken cancellationToken);
}
