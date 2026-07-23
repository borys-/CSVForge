using System.Text.RegularExpressions;

namespace CSVForge.Application.Sql;

public sealed partial class SqlCompletionService
{
    private static readonly string[] Keywords =
    [
        "SELECT", "FROM", "WHERE", "JOIN", "LEFT JOIN", "RIGHT JOIN", "INNER JOIN", "CROSS JOIN",
        "ON", "AS", "DISTINCT", "ALL", "AND", "OR", "NOT", "NULL", "IS NULL", "IS NOT NULL",
        "IN", "EXISTS", "BETWEEN", "LIKE", "GLOB", "CASE", "WHEN", "THEN", "ELSE", "END",
        "GROUP BY", "HAVING", "ORDER BY", "ASC", "DESC", "LIMIT", "OFFSET", "UNION", "UNION ALL",
        "WITH", "RECURSIVE", "INSERT INTO", "VALUES", "UPDATE", "SET", "DELETE FROM",
        "CREATE TABLE", "CREATE VIEW", "ALTER TABLE", "DROP TABLE", "DROP VIEW", "PRAGMA",
        "BEGIN", "COMMIT", "ROLLBACK", "RETURNING"
    ];

    private static readonly string[] Functions =
    [
        "COUNT", "SUM", "AVG", "MIN", "MAX", "TOTAL", "GROUP_CONCAT", "ABS", "ROUND",
        "COALESCE", "IFNULL", "NULLIF", "LENGTH", "LOWER", "UPPER", "TRIM", "LTRIM", "RTRIM",
        "SUBSTR", "REPLACE", "INSTR", "PRINTF", "DATE", "TIME", "DATETIME", "JULIANDAY",
        "STRFTIME", "RANDOM", "TYPEOF", "CAST", "ROW_NUMBER", "RANK", "DENSE_RANK"
    ];

    private static readonly HashSet<string> ReservedWords = new(
        Keywords.SelectMany(keyword => keyword.Split(' ')).Concat(["BY"]),
        StringComparer.OrdinalIgnoreCase);

    public SqlCompletionResult GetSuggestions(string text, int caretOffset, SqlSchemaSnapshot schema)
    {
        caretOffset = Math.Clamp(caretOffset, 0, text.Length);
        int replacementStart = caretOffset;
        while (replacementStart > 0 && IsIdentifierCharacter(text[replacementStart - 1]))
        {
            replacementStart--;
        }

        string prefix = text[replacementStart..caretOffset];
        string statement = CurrentStatement(text, caretOffset);
        string statementBeforeCaret = CurrentStatementBeforeCaret(text, caretOffset);
        Dictionary<string, SqlSchemaObject> aliases = ReadAliases(statement, schema);
        List<SqlSuggestion> candidates;

        if (replacementStart > 0 && text[replacementStart - 1] == '.')
        {
            string qualifier = ReadQualifier(text, replacementStart - 1);
            candidates = aliases.TryGetValue(qualifier, out SqlSchemaObject? source)
                ? source.Columns.Select(column => Column(column, source.Name)).ToList()
                : [];
        }
        else if (ExpectsTable(statementBeforeCaret, prefix))
        {
            candidates = schema.Objects.Select(SchemaObject).ToList();
        }
        else
        {
            IEnumerable<SqlSchemaObject> referencedObjects = aliases.Values.DistinctBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
            IEnumerable<SqlSchemaObject> columnSources = referencedObjects.Any() ? referencedObjects : schema.Objects;
            candidates =
            [
                .. Keywords.Select(keyword => new SqlSuggestion(keyword, "Słowo kluczowe SQLite", SqlSuggestionKind.Keyword)),
                .. Functions.Select(function => new SqlSuggestion($"{function}()", "Funkcja SQLite", SqlSuggestionKind.Function)),
                .. schema.Objects.Select(SchemaObject),
                .. columnSources.SelectMany(source => source.Columns.Select(column => Column(column, source.Name)))
            ];
        }

        IReadOnlyList<SqlSuggestion> suggestions = candidates
            .Where(item => MatchesPrefix(item, prefix))
            .DistinctBy(item => (item.Text, item.Kind))
            .OrderBy(item => Rank(item.Kind))
            .ThenBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToArray();

        return new SqlCompletionResult(replacementStart, caretOffset - replacementStart, suggestions);
    }

    public static string QuoteIdentifier(string identifier)
    {
        return SimpleIdentifierRegex().IsMatch(identifier) && !ReservedWords.Contains(identifier)
            ? identifier
            : $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static Dictionary<string, SqlSchemaObject> ReadAliases(string statement, SqlSchemaSnapshot schema)
    {
        Dictionary<string, SqlSchemaObject> aliases = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in TableReferenceRegex().Matches(statement))
        {
            string tableName = match.Groups["quoted"].Success
                ? match.Groups["quoted"].Value.Replace("\"\"", "\"", StringComparison.Ordinal)
                : match.Groups["table"].Value;
            SqlSchemaObject? source = schema.Objects.FirstOrDefault(
                item => string.Equals(item.Name, tableName, StringComparison.OrdinalIgnoreCase));
            if (source is null)
            {
                continue;
            }

            aliases[source.Name] = source;
            string alias = match.Groups["alias"].Value;
            if (!string.IsNullOrWhiteSpace(alias) && !ReservedWords.Contains(alias))
            {
                aliases[alias] = source;
            }
        }
        return aliases;
    }

    private static bool ExpectsTable(string statement, string prefix)
    {
        string beforePrefix = prefix.Length <= statement.Length ? statement[..^prefix.Length] : statement;
        return TableContextRegex().IsMatch(beforePrefix);
    }

    private static string CurrentStatement(string text, int caretOffset)
    {
        int start = caretOffset == 0 ? -1 : text.LastIndexOf(';', caretOffset - 1);
        int end = text.IndexOf(';', caretOffset);
        return text[(start + 1)..(end < 0 ? text.Length : end)];
    }

    private static string CurrentStatementBeforeCaret(string text, int caretOffset)
    {
        int start = caretOffset == 0 ? -1 : text.LastIndexOf(';', caretOffset - 1);
        return text[(start + 1)..caretOffset];
    }

    private static string ReadQualifier(string text, int dotOffset)
    {
        int start = dotOffset;
        while (start > 0 && IsIdentifierCharacter(text[start - 1]))
        {
            start--;
        }
        return text[start..dotOffset];
    }

    private static bool IsIdentifierCharacter(char character) =>
        char.IsLetterOrDigit(character) || character is '_' or '$';

    private static SqlSuggestion SchemaObject(SqlSchemaObject item) => new(
        QuoteIdentifier(item.Name),
        item.DisplayName is { Length: > 0 } displayName
            && !string.Equals(displayName, item.Name, StringComparison.OrdinalIgnoreCase)
                ? $"{(item.Kind == SqlSchemaObjectKind.Table ? "Tabela" : "Widok")} • {displayName}"
                : item.Kind == SqlSchemaObjectKind.Table ? "Tabela" : "Widok",
        item.Kind == SqlSchemaObjectKind.Table ? SqlSuggestionKind.Table : SqlSuggestionKind.View);

    private static SqlSuggestion Column(string column, string table) =>
        new(QuoteIdentifier(column), $"Kolumna • {table}", SqlSuggestionKind.Column);

    private static int Rank(SqlSuggestionKind kind) => kind switch
    {
        SqlSuggestionKind.Table or SqlSuggestionKind.View => 0,
        SqlSuggestionKind.Column => 1,
        SqlSuggestionKind.Keyword => 2,
        _ => 3
    };

    private static bool MatchesPrefix(SqlSuggestion suggestion, string prefix)
    {
        if (string.IsNullOrEmpty(prefix) || suggestion.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return suggestion.Kind is SqlSuggestionKind.Table or SqlSuggestionKind.View
            && (suggestion.Text.Contains(prefix, StringComparison.OrdinalIgnoreCase)
                || suggestion.Description.Contains(prefix, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex SimpleIdentifierRegex();

    [GeneratedRegex("""(?ix)\b(?:FROM|JOIN)\s+(?:"(?<quoted>(?:[^"]|"")+)"|(?<table>[A-Za-z_][\w$]*))(?:\s+(?:AS\s+)?(?<alias>(?!(?:WHERE|JOIN|LEFT|RIGHT|INNER|CROSS|ON|GROUP|ORDER|HAVING|LIMIT|UNION)\b)[A-Za-z_][\w$]*))?""")]
    private static partial Regex TableReferenceRegex();

    [GeneratedRegex(@"(?ix)\b(?:FROM|JOIN|UPDATE|INTO|DELETE\s+FROM)\s*$")]
    private static partial Regex TableContextRegex();
}
