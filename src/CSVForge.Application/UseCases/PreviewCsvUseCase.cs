using CSVForge.Application.Abstractions;
using CSVForge.Application.Csv;
using CSVForge.Application.Ports;
using CSVForge.Domain.Imports;

namespace CSVForge.Application.UseCases;

internal sealed class PreviewCsvUseCase(ICsvReader csvReader) : IPreviewCsvUseCase
{
    private const int PreviewRowLimit = 100;

    public Task<CsvPreview> ExecuteAsync(ImportRequest request, CancellationToken cancellationToken = default)
    {
        return csvReader.PreviewAsync(request, PreviewRowLimit, cancellationToken);
    }
}
