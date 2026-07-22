using System.IO;

namespace CSVForge.App.Wpf;

internal static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CSVForge");

    public static string LogsDirectory { get; } = Path.Combine(DataDirectory, "logs");
}
