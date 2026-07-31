using CSVForge.Application.Csv;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace CSVForge.App.Wpf;

public sealed class WatchedFolderSetting : INotifyPropertyChanged
{
    private bool _isEnabled;
    public WatchedFolderSetting(string path, bool isEnabled = true) { Path = System.IO.Path.GetFullPath(path); _isEnabled = isEnabled; }
    public string Path { get; }
    public bool IsEnabled { get => _isEnabled; set { if (_isEnabled == value) return; _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); PropertyChanged?.Invoke(this, new(nameof(Status))); } }
    public string Status => IsEnabled ? (Directory.Exists(Path) ? "Obserwowany" : "Folder nie istnieje") : "Wyłączony";
    public event PropertyChangedEventHandler? PropertyChanged;
}

internal enum CsvCandidateState { Preparing, Ready, Error }

internal sealed class CsvImportCandidate : INotifyPropertyChanged
{
    private CsvCandidateState _state;
    private string? _error;
    public CsvImportCandidate(string sourcePath, string folderPath)
    {
        SourcePath = sourcePath; FolderPath = folderPath; DisplayName = System.IO.Path.GetFileNameWithoutExtension(sourcePath); _state = CsvCandidateState.Preparing;
    }
    public string SourcePath { get; }
    public string FolderPath { get; }
    public string DisplayName { get; }
    public string? StagedPath { get; set; }
    public string? StagingDatabasePath { get; set; }
    public string? StagingTableName { get; set; }
    public CsvPreview? Preview { get; set; }
    public CsvCandidateState State { get => _state; set { _state = value; OnChanged(); OnChanged(nameof(Status)); OnChanged(nameof(StateBrush)); OnChanged(nameof(Symbol)); } }
    public string? Error { get => _error; set { _error = value; OnChanged(); OnChanged(nameof(ToolTip)); } }
    public string Status => State switch { CsvCandidateState.Preparing => "Przygotowywanie…", CsvCandidateState.Ready => "Gotowy do importu", _ => "Błąd przygotowania" };
    public string Symbol => State switch { CsvCandidateState.Preparing => "◌", CsvCandidateState.Ready => "●", _ => "!" };
    public Brush StateBrush => State switch { CsvCandidateState.Preparing => Brushes.DarkGoldenrod, CsvCandidateState.Ready => Brushes.DodgerBlue, _ => Brushes.Firebrick };
    public string ToolTip => Error is null ? SourcePath : $"{SourcePath}\n{Error}";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
