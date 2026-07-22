using CSVForge.Application.Export;

namespace CSVForge.Application.Abstractions;

public interface IExportTableUseCase
{
    Task<ExportResult> ExecuteAsync(ExportTableRequest request, CancellationToken cancellationToken = default);
}
