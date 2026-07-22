using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Application.Tables;

namespace CSVForge.Application.UseCases;

internal sealed class BrowseTableUseCase(ITableBrowser tableBrowser) : IBrowseTableUseCase
{
    public Task<TablePage> ExecuteAsync(BrowseTableRequest request, CancellationToken cancellationToken = default)
    {
        return tableBrowser.BrowseAsync(request, cancellationToken);
    }
}
