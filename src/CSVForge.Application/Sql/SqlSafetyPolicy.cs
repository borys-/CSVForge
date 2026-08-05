using System.Text.RegularExpressions;

namespace CSVForge.Application.Sql;

public enum SqlRiskLevel
{
    Safe,
    RequiresConfirmation,
    Forbidden
}

public sealed record SqlSafetyAssessment(SqlRiskLevel Risk, string UserMessage);

public static partial class SqlSafetyPolicy
{
    public static SqlSafetyAssessment Assess(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("SQL query is required.", nameof(sql));
        }

        string normalized = StripComments().Replace(sql, " ");
        if (ForbiddenCommand().IsMatch(normalized))
        {
            return new(SqlRiskLevel.Forbidden,
                "ATTACH DATABASE, load_extension oraz zapisujące polecenia PRAGMA są wyłączone ze względów bezpieczeństwa.");
        }

        if (HighRiskCommand().IsMatch(normalized))
        {
            return new(SqlRiskLevel.RequiresConfirmation,
                "Polecenie może trwale zmienić lub usunąć dane w aktywnym workspace.");
        }

        return new(SqlRiskLevel.Safe, string.Empty);
    }

    [GeneratedRegex(@"(?is)(--[^\r\n]*|/\*.*?\*/)")]
    private static partial Regex StripComments();

    [GeneratedRegex(@"(?ix)\b(ATTACH\s+(?:DATABASE\s+)?|DETACH\s+(?:DATABASE\s+)?|load_extension\s*\(|PRAGMA\s+[\w.]+\s*(?:=|\())")]
    private static partial Regex ForbiddenCommand();

    [GeneratedRegex(@"(?ix)\b(DROP\s+(?:TABLE|VIEW|INDEX|TRIGGER)|DELETE\s+FROM\s+[^;]+?(?:;|$)(?![^;]*\bWHERE\b)|VACUUM\s+INTO|TRUNCATE|REINDEX)\b")]
    private static partial Regex HighRiskCommand();
}
