namespace CSVForge.Application.Sql;

public enum SqlSchemaObjectKind
{
    Table,
    View
}

public sealed record SqlSchemaObject(
    string Name,
    SqlSchemaObjectKind Kind,
    IReadOnlyList<string> Columns,
    string? DisplayName = null);

public sealed record SqlSchemaSnapshot(IReadOnlyList<SqlSchemaObject> Objects)
{
    public static SqlSchemaSnapshot Empty { get; } = new([]);
}
