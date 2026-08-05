using CSVForge.Application.Export;

namespace CSVForge.Tests.Application;

public sealed class ExportNameTemplateTests
{
    [Fact]
    public void DefaultTemplate_ContainsRecordCountAndMinuteTimestamp()
    {
        DateTimeOffset timestamp = new(2026, 8, 5, 14, 7, 30, TimeSpan.FromHours(2));

        string result = ExportNameTemplate.ForFile(null, 1234, timestamp);

        Assert.Equal("1234_2026-08-05_14_07", result);
    }

    [Fact]
    public void TableName_IsNormalizedToSafeSqliteIdentifier()
    {
        DateTimeOffset timestamp = new(2026, 8, 5, 14, 7, 0, TimeSpan.Zero);

        string result = ExportNameTemplate.ForTable("{liczba_rekordów} raport/{data}", 12, timestamp);

        Assert.Equal("export_12_raport_2026_08_05", result);
    }

    [Fact]
    public void CustomTemplate_ReplacesAllSupportedVariables()
    {
        DateTimeOffset timestamp = new(2026, 8, 5, 9, 3, 0, TimeSpan.Zero);

        string result = ExportNameTemplate.Format("raport_{data}_{godzina}-{minuta}_{liczba_rekordów}", 42, timestamp);

        Assert.Equal("raport_2026-08-05_09-03_42", result);
    }
}
