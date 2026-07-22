using CSVForge.Application.Csv;
using CSVForge.Domain.Imports;

namespace CSVForge.Application.Ports;

public interface ICsvReader
{
    Task<CsvPreview> PreviewAsync(ImportRequest request, int rowLimit, CancellationToken cancellationToken);
}
