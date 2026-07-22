using CSVForge.Domain.Operations;

namespace CSVForge.Application.Abstractions;

public interface IFindDuplicatesUseCase
{
    Task<OperationResult> ExecuteAsync(DuplicateSearchRequest request, CancellationToken cancellationToken = default);
}
