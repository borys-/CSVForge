using CSVForge.Domain.Operations;

namespace CSVForge.Application.Ports;

public interface IDuplicateFinder
{
    Task<OperationResult> FindAsync(DuplicateSearchRequest request, CancellationToken cancellationToken);
}
