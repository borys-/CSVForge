namespace CSVForge.App.Wpf;

public sealed record PaletteCommand(
    string Name,
    string Category,
    string? Shortcut,
    bool IsEnabled,
    Action Execute);
