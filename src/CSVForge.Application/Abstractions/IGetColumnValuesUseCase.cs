using CSVForge.Application.Tables;

namespace CSVForge.Application.Abstractions;

public interface IGetColumnValuesUseCase
{
    Task<IReadOnlyList<ColumnValueOption>> ExecuteAsync(
        ColumnValuesRequest request,
        CancellationToken cancellationToken = default);
}
