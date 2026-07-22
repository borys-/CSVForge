namespace CSVForge.Application.Abstractions;

public interface IDeleteImportUseCase
{
    Task ExecuteAsync(Guid importId, CancellationToken cancellationToken = default);
}
