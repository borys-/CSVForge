using CSVForge.Application.Tables;

namespace CSVForge.Application.Ports;

public interface ITableBrowser
{
    Task<TablePage> BrowseAsync(BrowseTableRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ColumnValueOption>> GetColumnValuesAsync(
        ColumnValuesRequest request,
        CancellationToken cancellationToken);
}
