using CSVForge.Application.Abstractions;
using CSVForge.Application.Export;
using CSVForge.Application.Ports;

namespace CSVForge.Application.UseCases;

internal sealed class ExportTableUseCase(ITableExporter tableExporter) : IExportTableUseCase
{
    public Task<ExportResult> ExecuteAsync(ExportTableRequest request, CancellationToken cancellationToken = default)
    {
        return tableExporter.ExportAsync(request, cancellationToken);
    }
}
