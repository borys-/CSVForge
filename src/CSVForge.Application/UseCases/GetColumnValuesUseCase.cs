using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Application.Tables;

namespace CSVForge.Application.UseCases;

internal sealed class GetColumnValuesUseCase(ITableBrowser tableBrowser) : IGetColumnValuesUseCase
{
    public Task<IReadOnlyList<ColumnValueOption>> ExecuteAsync(
        ColumnValuesRequest request,
        CancellationToken cancellationToken = default) =>
        tableBrowser.GetColumnValuesAsync(request, cancellationToken);
}
