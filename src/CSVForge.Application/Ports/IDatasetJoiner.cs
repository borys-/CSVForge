using CSVForge.Domain.Operations;

namespace CSVForge.Application.Ports;

public interface IDatasetJoiner
{
    Task<OperationResult> JoinAsync(DatasetJoinRequest request, CancellationToken cancellationToken);
}
