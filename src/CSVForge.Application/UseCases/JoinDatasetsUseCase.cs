using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Domain.Operations;

namespace CSVForge.Application.UseCases;

internal sealed class JoinDatasetsUseCase(IDatasetJoiner datasetJoiner) : IJoinDatasetsUseCase
{
    public Task<OperationResult> ExecuteAsync(DatasetJoinRequest request, CancellationToken cancellationToken = default)
    {
        return datasetJoiner.JoinAsync(request, cancellationToken);
    }
}
