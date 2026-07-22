using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Domain.Operations;

namespace CSVForge.Application.UseCases;

internal sealed class FindDuplicatesUseCase(IDuplicateFinder duplicateFinder) : IFindDuplicatesUseCase
{
    public Task<OperationResult> ExecuteAsync(DuplicateSearchRequest request, CancellationToken cancellationToken = default)
    {
        return duplicateFinder.FindAsync(request, cancellationToken);
    }
}
