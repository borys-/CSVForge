using CSVForge.Domain.Imports;

namespace CSVForge.Application.Abstractions;

public interface IListImportedTablesUseCase
{
    Task<IReadOnlyList<CsvImport>> ExecuteAsync(CancellationToken cancellationToken = default);
}
