using CSVForge.Domain.Imports;

namespace CSVForge.Application.Csv;

public sealed record CsvPreview(
    IReadOnlyList<CsvColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyList<ImportError> Errors);
