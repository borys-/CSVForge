using CSVForge.Application.Csv;
using CSVForge.Domain.Imports;

namespace CSVForge.Application.Ports;

public interface ICsvStagingService
{
    Task<CsvStagingResult> StageAsync(ImportRequest request, CancellationToken cancellationToken);
}

public sealed record CsvStagingResult(string DatabasePath, string TableName, CsvPreview Preview, long RowCount);
