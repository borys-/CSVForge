using CSVForge.Domain.Imports;

namespace CSVForge.Application.Ports;

public interface ICsvImporter
{
    Task<ImportResult> ImportAsync(ImportRequest request, IProgress<ImportProgress>? progress, CancellationToken cancellationToken);
}
