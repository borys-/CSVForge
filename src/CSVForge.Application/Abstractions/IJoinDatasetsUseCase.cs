using CSVForge.Domain.Operations;

namespace CSVForge.Application.Abstractions;

public interface IJoinDatasetsUseCase
{
    Task<OperationResult> ExecuteAsync(DatasetJoinRequest request, CancellationToken cancellationToken = default);
}
