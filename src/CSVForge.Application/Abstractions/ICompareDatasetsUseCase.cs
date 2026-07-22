using CSVForge.Domain.Operations;

namespace CSVForge.Application.Abstractions;

public interface ICompareDatasetsUseCase
{
    Task<OperationResult> ExecuteAsync(DatasetCompareRequest request, CancellationToken cancellationToken = default);
}
