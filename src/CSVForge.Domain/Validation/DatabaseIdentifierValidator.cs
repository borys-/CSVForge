using System.Text.RegularExpressions;

namespace CSVForge.Domain.Validation;

public static partial class DatabaseIdentifierValidator
{
    private const int MaxIdentifierLength = 64;

    public static bool IsValidTableName(string value)
    {
        return IsValidIdentifier(value);
    }

    public static bool IsValidColumnName(string value)
    {
        return IsValidIdentifier(value);
    }

    public static void EnsureValidTableName(string value)
    {
        EnsureValidIdentifier(value, "Table name");
    }

    public static void EnsureValidColumnName(string value)
    {
        EnsureValidIdentifier(value, "Column name");
    }

    private static bool IsValidIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxIdentifierLength
            && IdentifierPattern().IsMatch(value);
    }

    private static void EnsureValidIdentifier(string value, string label)
    {
        if (!IsValidIdentifier(value))
        {
            throw new ArgumentException(
                $"{label} must start with a letter or underscore and contain only letters, digits, and underscores.",
                nameof(value));
        }
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierPattern();
}
