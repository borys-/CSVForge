using System.Globalization;
using System.Text;
using CSVForge.Application.Csv;
using CSVForge.Application.Ports;
using CSVForge.Domain.Imports;
using CsvHelper;
using CsvHelper.Configuration;

namespace CSVForge.Infrastructure.Csv;

internal sealed class CsvReaderService : ICsvReader
{
    public async Task<CsvPreview> PreviewAsync(ImportRequest request, int rowLimit, CancellationToken cancellationToken)
    {
        if (!File.Exists(request.FilePath))
        {
            throw new FileNotFoundException("CSV file does not exist.", request.FilePath);
        }

        Encoding encoding = await CsvEncodingHelper.ResolveAsync(request, cancellationToken);
        char delimiter = request.Delimiter ?? await CsvImportNameHelper.DetectDelimiterAsync(request.FilePath, encoding, cancellationToken);

        await using FileStream stream = File.OpenRead(request.FilePath);
        using StreamReader reader = new(stream, encoding, detectEncodingFromByteOrderMarks: true);
        using CsvReader csv = new(reader, CreateConfiguration(delimiter));

        List<CsvColumn> columns = [];
        List<IReadOnlyList<string>> rows = [];
        List<ImportError> errors = [];

        try
        {
            if (!await csv.ReadAsync())
            {
                return new CsvPreview(columns, rows, errors);
            }

            string[] firstRecord = csv.Parser.Record ?? [];
            string[]? secondRecord = null;
            bool hasHeader = request.HasHeader;
            if (request.AutoDetectHeader && await csv.ReadAsync())
            {
                secondRecord = csv.Parser.Record ?? [];
                hasHeader = CsvHeaderDetector.LooksLikeHeader(firstRecord, secondRecord);
            }

            if (hasHeader)
            {
                columns.AddRange(CreateColumns(firstRecord));
            }
            else
            {
                columns.AddRange(CreateGeneratedColumns(firstRecord.Length));
                rows.Add(firstRecord);
            }
            if (secondRecord is not null)
            {
                rows.Add(secondRecord);
            }

            while (rows.Count < rowLimit && await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(csv.Parser.Record ?? []);
            }
        }
        catch (Exception ex) when (ex is CsvHelperException or DecoderFallbackException)
        {
            errors.Add(new ImportError(csv.Parser.Row, ex.Message, csv.Parser.RawRecord));
        }

        return new CsvPreview(columns, rows, errors);
    }

    private static CsvConfiguration CreateConfiguration(char delimiter)
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter.ToString(),
            BadDataFound = null,
            MissingFieldFound = null
        };
    }

    private static IReadOnlyList<CsvColumn> CreateColumns(IReadOnlyList<string> headers)
    {
        IReadOnlyList<string> normalizedNames = CsvImportNameHelper.NormalizeColumns(headers);
        return headers.Select((header, index) => new CsvColumn(
                string.IsNullOrWhiteSpace(header) ? $"Column{index + 1}" : header,
                normalizedNames[index],
                index))
            .ToArray();
    }

    private static IReadOnlyList<CsvColumn> CreateGeneratedColumns(int count)
    {
        return CsvImportNameHelper.GenerateColumns(count)
            .Select((name, index) => new CsvColumn(name, name, index))
            .ToArray();
    }
}
