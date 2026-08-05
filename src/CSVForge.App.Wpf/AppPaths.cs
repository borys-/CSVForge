using System.IO;

namespace CSVForge.App.Wpf;

internal static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CSVForge");

    public static string LogsDirectory { get; } = Path.Combine(DataDirectory, "logs");

    public static string StagingDirectory { get; } = Path.Combine(Path.GetTempPath(), "CSVForge", "staging");

    public static void CleanupOrphanedTemporaryFiles()
    {
        CleanupDirectory(StagingDirectory, TimeSpan.FromDays(1));
        CleanupDirectory(DataDirectory, TimeSpan.FromDays(7));
    }

    private static void CleanupDirectory(string directory, TimeSpan minimumAge)
    {
        if (!Directory.Exists(directory)) return;
        DateTime cutoff = DateTime.UtcNow - minimumAge;
        foreach (string path in Directory.EnumerateFiles(directory, "*.tmp", SearchOption.AllDirectories))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff) File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Serilog.Log.Warning(ex, "Could not remove orphaned temporary file {TemporaryPath}", path);
            }
        }
    }
}
