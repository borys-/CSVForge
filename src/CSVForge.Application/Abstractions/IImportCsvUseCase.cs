using CSVForge.Domain.Imports;

namespace CSVForge.Application.Abstractions;

public interface IImportCsvUseCase
{
    Task<ImportResult> ExecuteAsync(ImportRequest request, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default);
}
