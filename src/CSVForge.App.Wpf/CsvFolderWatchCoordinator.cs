using CSVForge.Application.Ports;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Data.Sqlite;

namespace CSVForge.App.Wpf;

internal sealed class CsvFolderWatchCoordinator : IDisposable
{
    private readonly ICsvStagingService _stagingService;
    private readonly Action<CsvImportCandidate> _candidateChanged;
    private readonly ConcurrentDictionary<string, CsvImportCandidate> _candidates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _staging = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly SemaphoreSlim _stagingSlots = new(2, 2);
    private readonly CancellationTokenSource _shutdown = new();
    private HashSet<string> _importedPaths = new(StringComparer.OrdinalIgnoreCase);
    private List<WatchedFolderSetting> _settings = [];
    private PeriodicTimer? _scanTimer;

    public CsvFolderWatchCoordinator(ICsvStagingService stagingService, Action<CsvImportCandidate> candidateChanged)
    {
        _stagingService = stagingService; _candidateChanged = candidateChanged;
    }

    public IReadOnlyCollection<CsvImportCandidate> Candidates => _candidates.Values.ToArray();

    public void Configure(IEnumerable<WatchedFolderSetting> settings, IEnumerable<string> importedPaths)
    {
        StopWatchers();
        foreach (CsvImportCandidate candidate in _candidates.Values.ToArray()) Remove(candidate.SourcePath);
        _settings = settings.Select(item => new WatchedFolderSetting(item.Path, item.IsEnabled)).ToList();
        _importedPaths = importedPaths.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        RemoveInvalidCandidates();
        foreach (WatchedFolderSetting setting in _settings.Where(item => item.IsEnabled && Directory.Exists(item.Path)))
        {
            FileSystemWatcher watcher = new(setting.Path) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite };
            watcher.Created += (_, e) => Queue(e.FullPath, setting.Path);
            watcher.Changed += (_, e) => Queue(e.FullPath, setting.Path, force: true);
            watcher.Renamed += (_, e) => Queue(e.FullPath, setting.Path);
            watcher.Deleted += (_, e) => Remove(e.FullPath);
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
        Scan();
        _scanTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        _ = PeriodicScanAsync(_scanTimer, _shutdown.Token);
    }

    public void RefreshImportedPaths(IEnumerable<string> importedPaths)
    {
        _importedPaths = importedPaths.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        RemoveInvalidCandidates();
        Scan();
    }

    public void Retry(CsvImportCandidate candidate)
    {
        if (candidate.StagedPath is not null) { TryDelete(candidate.StagedPath); candidate.StagedPath = null; }
        candidate.State = CsvCandidateState.Preparing; candidate.Error = null; Queue(candidate.SourcePath, candidate.FolderPath, force: true);
    }

    public void Complete(CsvImportCandidate candidate) => Remove(candidate.SourcePath);

    private void Scan()
    {
        foreach (WatchedFolderSetting setting in _settings.Where(item => item.IsEnabled && Directory.Exists(item.Path)))
        {
            foreach (string path in Directory.EnumerateFiles(setting.Path, "*", SearchOption.TopDirectoryOnly).Where(IsImportFile)) Queue(path, setting.Path);
        }
    }

    private void Queue(string path, string folder, bool force = false)
    {
        if (!IsImportFile(path)) return;
        string normalized = Normalize(path);
        if (_importedPaths.Contains(normalized)) { Remove(path); return; }
        CsvImportCandidate candidate = _candidates.GetOrAdd(normalized, _ => new CsvImportCandidate(Path.GetFullPath(path), folder));
        _candidateChanged(candidate);
        if ((force || candidate.State == CsvCandidateState.Preparing && candidate.StagedPath is null) && _staging.TryAdd(normalized, 0))
            _ = StageAsync(candidate, normalized, _shutdown.Token);
    }

    private async Task StageAsync(CsvImportCandidate candidate, string normalizedPath, CancellationToken cancellationToken)
    {
        await _stagingSlots.WaitAsync(cancellationToken);
        try
        {
            await WaitUntilStableAsync(candidate.SourcePath, cancellationToken);
            if (string.Equals(Path.GetExtension(candidate.SourcePath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                candidate.State = CsvCandidateState.Ready;
                candidate.Error = null;
                return;
            }
            string stagingDirectory = Path.Combine(Path.GetTempPath(), "CSVForge", "staging");
            Directory.CreateDirectory(stagingDirectory);
            string stagedPath = Path.Combine(stagingDirectory, $"{Guid.NewGuid():N}.csv");
            File.Copy(candidate.SourcePath, stagedPath, overwrite: true);
            if (!_candidates.TryGetValue(normalizedPath, out CsvImportCandidate? current) || !ReferenceEquals(current, candidate))
            {
                TryDelete(stagedPath);
                return;
            }
            if (candidate.StagedPath is not null) TryDelete(candidate.StagedPath);
            candidate.StagedPath = stagedPath;
            CSVForge.Domain.Imports.ImportRequest request = new(stagedPath, candidate.DisplayName, true, null, null, AutoDetectHeader: true, SourcePath: candidate.SourcePath);
            CsvStagingResult staging = await _stagingService.StageAsync(request, cancellationToken);
            candidate.StagingDatabasePath = staging.DatabasePath;
            candidate.StagingTableName = staging.TableName;
            candidate.Preview = staging.Preview;
            candidate.State = CsvCandidateState.Ready;
            candidate.Error = null;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Could not stage watched CSV file {CsvPath}", candidate.SourcePath);
            candidate.Error = UserErrorMessages.From(ex);
            candidate.State = CsvCandidateState.Error;
        }
        finally { _staging.TryRemove(normalizedPath, out _); _candidateChanged(candidate); _stagingSlots.Release(); }
    }

    private static async Task WaitUntilStableAsync(string path, CancellationToken cancellationToken)
    {
        long previousLength = -1; DateTime previousWrite = DateTime.MinValue;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                FileInfo info = new(path);
                if (info.Length == previousLength && info.LastWriteTimeUtc == previousWrite)
                {
                    using FileStream _ = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return;
                }
                previousLength = info.Length; previousWrite = info.LastWriteTimeUtc;
            }
            await Task.Delay(500, cancellationToken);
        }
        throw new IOException("Plik nie jest jeszcze gotowy do odczytu.");
    }

    private static bool IsImportFile(string path)
    {
        string extension = Path.GetExtension(path);
        return File.Exists(path) && (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase));
    }

    private async Task PeriodicScanAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try { while (await timer.WaitForNextTickAsync(cancellationToken)) Scan(); } catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
    }

    private void RemoveInvalidCandidates()
    {
        HashSet<string> activeFolders = _settings.Where(item => item.IsEnabled).Select(item => Normalize(item.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (CsvImportCandidate candidate in _candidates.Values.Where(item => _importedPaths.Contains(Normalize(item.SourcePath)) || !File.Exists(item.SourcePath) || !activeFolders.Contains(Normalize(item.FolderPath))).ToArray()) Remove(candidate.SourcePath);
    }

    private void Remove(string path)
    {
        if (_candidates.TryRemove(Normalize(path), out CsvImportCandidate? candidate))
        {
            if (candidate.StagedPath is not null) TryDelete(candidate.StagedPath);
            if (candidate.StagingDatabasePath is not null) TryDelete(candidate.StagingDatabasePath);
            _candidateChanged(candidate);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void StopWatchers() { _scanTimer?.Dispose(); _scanTimer = null; foreach (FileSystemWatcher watcher in _watchers) watcher.Dispose(); _watchers.Clear(); }
    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
    public void Dispose() { _shutdown.Cancel(); StopWatchers(); foreach (CsvImportCandidate item in _candidates.Values.ToArray()) Remove(item.SourcePath); _stagingSlots.Dispose(); _shutdown.Dispose(); }
}
