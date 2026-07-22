using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Domain.Operations;

namespace CSVForge.Application.UseCases;

internal sealed class CompareDatasetsUseCase(IDatasetComparer datasetComparer) : ICompareDatasetsUseCase
{
    public Task<OperationResult> ExecuteAsync(DatasetCompareRequest request, CancellationToken cancellationToken = default)
    {
        return datasetComparer.CompareAsync(request, cancellationToken);
    }
}
