using CSVForge.Application.Export;

namespace CSVForge.Application.Abstractions;

public interface ICreateTableFromResultUseCase
{
    Task<CreateTableFromResultResult> ExecuteAsync(
        CreateTableFromResultRequest request,
        CancellationToken cancellationToken = default);
}
