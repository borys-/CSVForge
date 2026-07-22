namespace CSVForge.Application.Abstractions;

public interface IRenameImportUseCase
{
    Task ExecuteAsync(Guid importId, string displayName, CancellationToken cancellationToken = default);
}
