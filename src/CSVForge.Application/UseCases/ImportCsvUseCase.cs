using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Domain.Imports;

namespace CSVForge.Application.UseCases;

internal sealed class ImportCsvUseCase(ICsvImporter csvImporter) : IImportCsvUseCase
{
    public Task<ImportResult> ExecuteAsync(ImportRequest request, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return csvImporter.ImportAsync(request, progress, cancellationToken);
    }
}
