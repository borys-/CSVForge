using CSVForge.Application.Tables;

namespace CSVForge.Application.Abstractions;

public interface IBrowseTableUseCase
{
    Task<TablePage> ExecuteAsync(BrowseTableRequest request, CancellationToken cancellationToken = default);
}
