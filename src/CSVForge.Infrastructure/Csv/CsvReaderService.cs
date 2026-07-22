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
    private static readonly char[] CandidateDelimiters = [';', ',', '\t'];

    public async Task<CsvPreview> PreviewAsync(ImportRequest request, int rowLimit, CancellationToken cancellationToken)
    {
        if (!File.Exists(request.FilePath))
        {
            throw new FileNotFoundException("CSV file does not exist.", request.FilePath);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Encoding encoding = ResolveEncoding(request);
        char delimiter = request.Delimiter ?? await DetectDelimiterAsync(request.FilePath, encoding, cancellationToken);

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
            if (request.HasHeader)
            {
                columns.AddRange(CreateColumns(firstRecord));
            }
            else
            {
                columns.AddRange(CreateGeneratedColumns(firstRecord.Length));
                rows.Add(firstRecord);
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

    private static Encoding ResolveEncoding(ImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EncodingName))
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        }

        return Encoding.GetEncoding(request.EncodingName);
    }

    private static async Task<char> DetectDelimiterAsync(string filePath, Encoding encoding, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(filePath);
        using StreamReader reader = new(stream, encoding, detectEncodingFromByteOrderMarks: true);

        string sample = string.Empty;
        for (int i = 0; i < 5; i++)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            sample += line;
        }

        return CandidateDelimiters
            .Select(delimiter => new { Delimiter = delimiter, Count = sample.Count(character => character == delimiter) })
            .OrderByDescending(candidate => candidate.Count)
            .FirstOrDefault(candidate => candidate.Count > 0)
            ?.Delimiter ?? ',';
    }

    private static IReadOnlyList<CsvColumn> CreateColumns(IReadOnlyList<string> headers)
    {
        Dictionary<string, int> seenNames = new(StringComparer.OrdinalIgnoreCase);
        List<CsvColumn> columns = [];

        for (int i = 0; i < headers.Count; i++)
        {
            string originalName = string.IsNullOrWhiteSpace(headers[i]) ? $"Column{i + 1}" : headers[i];
            string normalizedName = NormalizeColumnName(originalName);
            string uniqueName = MakeUnique(normalizedName, seenNames);

            columns.Add(new CsvColumn(originalName, uniqueName, i));
        }

        return columns;
    }

    private static IReadOnlyList<CsvColumn> CreateGeneratedColumns(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new CsvColumn($"Column{index}", $"Column{index}", index - 1))
            .ToArray();
    }

    private static string NormalizeColumnName(string value)
    {
        StringBuilder builder = new();

        foreach (char character in value.Trim())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        string normalized = builder.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "Column";
        }

        if (char.IsDigit(normalized[0]))
        {
            normalized = $"Column_{normalized}";
        }

        return normalized;
    }

    private static string MakeUnique(string name, IDictionary<string, int> seenNames)
    {
        if (!seenNames.TryGetValue(name, out int count))
        {
            seenNames[name] = 1;
            return name;
        }

        count++;
        seenNames[name] = count;
        return $"{name}_{count}";
    }
}
