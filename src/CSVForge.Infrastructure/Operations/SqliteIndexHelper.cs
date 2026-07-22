using System.Security.Cryptography;
using System.Text;
using CSVForge.Infrastructure.Csv;
using Microsoft.Data.Sqlite;

namespace CSVForge.Infrastructure.Operations;

internal static class SqliteIndexHelper
{
    public static async Task EnsureAsync(SqliteConnection connection, string tableName, IReadOnlyList<string> columns, CancellationToken cancellationToken)
    {
        string signature = $"{tableName}|{string.Join("|", columns)}";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)))[..16].ToLowerInvariant();
        string indexName = $"idx_csvforge_{hash}";
        string columnList = string.Join(", ", columns.Select(CsvImportNameHelper.QuoteIdentifier));

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"CREATE INDEX IF NOT EXISTS {CsvImportNameHelper.QuoteIdentifier(indexName)} ON {CsvImportNameHelper.QuoteIdentifier(tableName)} ({columnList});";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
