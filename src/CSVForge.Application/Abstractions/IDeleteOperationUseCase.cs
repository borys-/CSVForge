namespace CSVForge.Application.Abstractions;

public interface IDeleteOperationUseCase
{
    Task ExecuteAsync(Guid operationId, CancellationToken cancellationToken = default);
}
