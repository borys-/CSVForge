namespace CSVForge.Domain.Workspaces;

public sealed record Workspace(
    string Path,
    string Name,
    DateTimeOffset CreatedAt);
