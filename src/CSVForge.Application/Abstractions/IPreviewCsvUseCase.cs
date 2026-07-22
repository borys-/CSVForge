using CSVForge.Application.Csv;
using CSVForge.Domain.Imports;

namespace CSVForge.Application.Abstractions;

public interface IPreviewCsvUseCase
{
    Task<CsvPreview> ExecuteAsync(ImportRequest request, CancellationToken cancellationToken = default);
}
