using System.Text;
using CSVForge.Domain.Imports;

namespace CSVForge.Infrastructure.Csv;

internal static class CsvEncodingHelper
{
    public static async Task<Encoding> ResolveAsync(ImportRequest request, CancellationToken cancellationToken)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        if (!string.IsNullOrWhiteSpace(request.EncodingName))
        {
            return Encoding.GetEncoding(request.EncodingName);
        }

        byte[] buffer = new byte[64 * 1024];
        await using FileStream stream = File.OpenRead(request.FilePath);
        int length = await stream.ReadAsync(buffer, cancellationToken);
        if (length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
        }

        try
        {
            _ = new UTF8Encoding(false, true).GetString(buffer, 0, length);
            return new UTF8Encoding(false, true);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding("windows-1250");
        }
    }
}
