namespace CSVForge.Domain.Imports;

public sealed record CsvColumnMapping(
    int SourceIndex,
    string Name,
    CsvColumnDataType DataType,
    bool Include = true);
