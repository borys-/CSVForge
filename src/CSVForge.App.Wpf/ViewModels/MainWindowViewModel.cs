namespace CSVForge.App.Wpf.ViewModels;

internal sealed class MainWindowViewModel
{
    public WorkspaceUiState Workspace { get; } = new();
    public ImportUiState Import { get; } = new();
    public TableUiState Table { get; } = new();
    public OperationUiState Operation { get; } = new();
    public SqlUiState Sql { get; } = new();
    public SettingsUiState Settings { get; } = new();
}

internal sealed class WorkspaceUiState : ObservableObject
{
    private string? currentPath;
    public string? CurrentPath { get => currentPath; set => Set(ref currentPath, value); }
}

internal sealed class ImportUiState : ObservableObject
{
    private int activeCount;
    public int ActiveCount { get => activeCount; set => Set(ref activeCount, value); }
}

internal sealed class TableUiState : ObservableObject
{
    private string? selectedTable;
    public string? SelectedTable { get => selectedTable; set => Set(ref selectedTable, value); }
}

internal sealed class SqlUiState : ObservableObject
{
    private string status = "Ctrl+Enter wykonuje zapytanie";
    public string Status { get => status; set => Set(ref status, value); }
}

internal sealed class SettingsUiState : ObservableObject
{
    private bool filesPanelExpanded = true;
    public bool FilesPanelExpanded { get => filesPanelExpanded; set => Set(ref filesPanelExpanded, value); }
}

internal sealed class OperationUiState : ObservableObject, IDisposable
{
    private CancellationTokenSource? cancellation;
    private bool isBusy;
    private string status = string.Empty;

    public bool IsBusy { get => isBusy; private set => Set(ref isBusy, value); }
    public string Status { get => status; set => Set(ref status, value); }
    public CancellationToken Token => cancellation?.Token ?? CancellationToken.None;

    public CancellationToken Begin(string initialStatus)
    {
        if (IsBusy) return Token;
        cancellation = new CancellationTokenSource();
        Status = initialStatus;
        IsBusy = true;
        return cancellation.Token;
    }

    public void Cancel() => cancellation?.Cancel();

    public void Complete()
    {
        cancellation?.Dispose();
        cancellation = null;
        IsBusy = false;
    }

    public void Dispose()
    {
        cancellation?.Cancel();
        Complete();
    }
}
