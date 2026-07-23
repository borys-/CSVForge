namespace CSVForge.Application.Sql;

public enum SqlSuggestionKind
{
    Keyword,
    Function,
    Table,
    View,
    Column
}

public sealed record SqlSuggestion(string Text, string Description, SqlSuggestionKind Kind);

public sealed record SqlCompletionResult(
    int ReplacementStart,
    int ReplacementLength,
    IReadOnlyList<SqlSuggestion> Suggestions);
