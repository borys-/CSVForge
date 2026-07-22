using CSVForge.Application.Export;

namespace CSVForge.Application.Ports;

public interface ITableExporter
{
    Task<ExportResult> ExportAsync(ExportTableRequest request, CancellationToken cancellationToken);
}
