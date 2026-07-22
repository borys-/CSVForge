using CSVForge.Domain.Operations;

namespace CSVForge.Application.Ports;

public interface IDatasetComparer
{
    Task<OperationResult> CompareAsync(DatasetCompareRequest request, CancellationToken cancellationToken);
}
