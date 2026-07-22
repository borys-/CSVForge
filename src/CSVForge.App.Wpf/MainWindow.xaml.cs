using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Export;
using CSVForge.Application.Operations;
using CSVForge.Application.Csv;
using CSVForge.Application.Tables;
using CSVForge.Domain.Imports;
using CSVForge.Domain.Operations;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace CSVForge.App.Wpf;

public partial class MainWindow : Window
{
    private const int PageSize = 200;
    private static readonly string RecentWorkspacesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CSVForge", "recent-workspaces.json");

    private readonly ICreateWorkspaceUseCase _createWorkspace;
    private readonly IOpenWorkspaceUseCase _openWorkspace;
    private readonly IImportCsvUseCase _importCsv;
    private readonly IPreviewCsvUseCase _previewCsv;
    private readonly IListImportedTablesUseCase _listImportedTables;
    private readonly IBrowseTableUseCase _browseTable;
    private readonly IFindDuplicatesUseCase _findDuplicates;
    private readonly ICompareDatasetsUseCase _compareDatasets;
    private readonly IJoinDatasetsUseCase _joinDatasets;
    private readonly IExportTableUseCase _exportTable;
    private readonly IListOperationsUseCase _listOperations;
    private readonly IDeleteImportUseCase _deleteImport;
    private readonly IRenameImportUseCase _renameImport;
    private readonly IDeleteOperationUseCase _deleteOperation;

    private CsvImport? _selectedImport;
    private string? _adHocTableName;
    private int _pageOffset;
    private long _totalRows;
    private CancellationTokenSource? _operationCancellation;

    public MainWindow(
        ICreateWorkspaceUseCase createWorkspace,
        IOpenWorkspaceUseCase openWorkspace,
        IImportCsvUseCase importCsv,
        IPreviewCsvUseCase previewCsv,
        IListImportedTablesUseCase listImportedTables,
        IBrowseTableUseCase browseTable,
        IFindDuplicatesUseCase findDuplicates,
        ICompareDatasetsUseCase compareDatasets,
        IJoinDatasetsUseCase joinDatasets,
        IExportTableUseCase exportTable,
        IListOperationsUseCase listOperations,
        IDeleteImportUseCase deleteImport,
        IRenameImportUseCase renameImport,
        IDeleteOperationUseCase deleteOperation)
    {
        _createWorkspace = createWorkspace;
        _openWorkspace = openWorkspace;
        _importCsv = importCsv;
        _previewCsv = previewCsv;
        _listImportedTables = listImportedTables;
        _browseTable = browseTable;
        _findDuplicates = findDuplicates;
        _compareDatasets = compareDatasets;
        _joinDatasets = joinDatasets;
        _exportTable = exportTable;
        _listOperations = listOperations;
        _deleteImport = deleteImport;
        _renameImport = renameImport;
        _deleteOperation = deleteOperation;

        InitializeComponent();
        LoadRecentWorkspaces();
    }

    private async void OpenWorkspace_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            string path = WorkspacePathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSVForge", "workspace.db");
                WorkspacePathTextBox.Text = path;
            }

            if (File.Exists(path))
            {
                await _openWorkspace.ExecuteAsync(path);
            }
            else
            {
                await _createWorkspace.ExecuteAsync(path);
            }

            WorkspaceStatusText.Text = path;
            SaveRecentWorkspace(path);
            await RefreshImportsAsync();
            await RefreshOperationsAsync();
        }, "Workspace gotowy");
    }

    private void ChooseCsv_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Wybierz plik CSV"
        };

        if (dialog.ShowDialog(this) == true)
        {
            CsvPathTextBox.Text = dialog.FileName;
        }
    }

    private async void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async cancellationToken =>
        {
            string path = CsvPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this, "Wybierz plik CSV do importu.", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string displayName = Path.GetFileNameWithoutExtension(path);
            Progress<ImportProgress> progress = new(value => StatusText.Text = $"Import: {value.ProcessedRows} wierszy");
            ImportResult result = await _importCsv.ExecuteAsync(new ImportRequest(path, displayName, true, null, null), progress, cancellationToken);
            await RefreshImportsAsync();
            SelectImport(result.Import.Id);
        }, "Import zakończony");
    }

    private async void PreviewCsv_Click(object sender, RoutedEventArgs e)
    {
        string path = CsvPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show(this, "Wybierz plik CSV do podglądu.", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await RunUiActionAsync(async cancellationToken =>
        {
            CsvPreview preview = await _previewCsv.ExecuteAsync(new ImportRequest(path, Path.GetFileNameWithoutExtension(path), true, null, null), cancellationToken);
            List<Dictionary<string, string?>> rows = preview.Rows.Select(row => preview.Columns
                .Select((column, index) => new { column.Name, Value = index < row.Count ? row[index] : null })
                .ToDictionary(item => item.Name, item => item.Value)).ToList();
            DataGrid.ItemsSource = rows;
            TableTitleText.Text = $"Podgląd: {Path.GetFileName(path)} ({preview.Rows.Count} wierszy)";
            PageStatusText.Text = preview.Errors.Count == 0 ? "CSV poprawny" : $"Błędy: {preview.Errors.Count}";
        }, "Podgląd gotowy");
    }

    private async void ImportsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedImport = ImportsListBox.SelectedItem as CsvImport;
        _adHocTableName = null;
        _pageOffset = 0;
        ImportNameTextBox.Text = _selectedImport?.DisplayName ?? string.Empty;
        DuplicateColumnComboBox.ItemsSource = _selectedImport?.Columns.Select(column => column.Name).ToArray();
        DuplicateColumnComboBox.SelectedIndex = DuplicateColumnComboBox.Items.Count > 0 ? 0 : -1;
        await RefreshSelectedTableAsync();
    }

    private async void RefreshTable_Click(object sender, RoutedEventArgs e)
    {
        _pageOffset = 0;
        await RefreshSelectedTableAsync();
    }

    private async void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        _pageOffset = Math.Max(0, _pageOffset - PageSize);
        await RefreshSelectedTableAsync();
    }

    private async void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_pageOffset + PageSize >= _totalRows)
        {
            return;
        }

        _pageOffset += PageSize;
        await RefreshSelectedTableAsync();
    }

    private async void FindDuplicates_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedImport is null || DuplicateColumnComboBox.SelectedItem is not string columnName)
        {
            return;
        }

        await RunUiActionAsync(async () =>
        {
            OperationResult result = await _findDuplicates.ExecuteAsync(new DuplicateSearchRequest(
                _selectedImport.TableName,
                [columnName],
                DuplicateSearchMode.AllDuplicateRows,
                true));

            _adHocTableName = result.ResultTableName;
            _pageOffset = 0;
            await RefreshSelectedTableAsync();
            await RefreshOperationsAsync();
        }, "Duplikaty gotowe");
    }

    private async void CompareTables_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedImport is null ||
            CompareTableComboBox.SelectedItem is not CsvImport rightImport ||
            DuplicateColumnComboBox.SelectedItem is not string columnName)
        {
            return;
        }

        await RunUiActionAsync(async () =>
        {
            OperationResult result = await _compareDatasets.ExecuteAsync(new DatasetCompareRequest(
                _selectedImport.TableName,
                rightImport.TableName,
                [columnName],
                [columnName],
                DatasetCompareMode.AllWithStatus));

            _adHocTableName = result.ResultTableName;
            _pageOffset = 0;
            await RefreshSelectedTableAsync();
            await RefreshOperationsAsync();
        }, "Porównanie gotowe");
    }

    private async void JoinTables_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedImport is null ||
            CompareTableComboBox.SelectedItem is not CsvImport rightImport ||
            DuplicateColumnComboBox.SelectedItem is not string columnName)
        {
            return;
        }

        if (rightImport.Columns.All(column => !string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, $"Tabela po prawej stronie nie ma kolumny '{columnName}'.", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await RunUiActionAsync(async () =>
        {
            OperationResult result = await _joinDatasets.ExecuteAsync(new DatasetJoinRequest(
                _selectedImport.TableName,
                rightImport.TableName,
                [columnName],
                [columnName],
                _selectedImport.Columns.Select(column => column.Name).ToArray(),
                rightImport.Columns.Select(column => column.Name).ToArray(),
                DatasetJoinType.Left));

            _adHocTableName = result.ResultTableName;
            _pageOffset = 0;
            await RefreshSelectedTableAsync();
            await RefreshOperationsAsync();
        }, "Połączenie gotowe");
    }

    private async void ExportTable_Click(object sender, RoutedEventArgs e)
    {
        string? tableName = _adHocTableName ?? _selectedImport?.TableName;
        if (tableName is null)
        {
            MessageBox.Show(this, "Wybierz tabelę do eksportu.", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SaveFileDialog dialog = new()
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = $"{tableName}.csv",
            Title = "Eksportuj tabelę do CSV"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        char delimiter = ExportDelimiterComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag.Length > 0
            ? tag[0]
            : ';';

        await RunUiActionAsync(async cancellationToken =>
        {
            ExportResult result = await _exportTable.ExecuteAsync(new ExportTableRequest(tableName, dialog.FileName, delimiter, true), cancellationToken);
            MessageBox.Show(this, $"Wyeksportowano {result.ExportedRows} wierszy do:\n{result.FilePath}", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
        }, "Eksport zakończony");
    }

    private async Task RefreshImportsAsync()
    {
        IReadOnlyList<CsvImport> imports = await _listImportedTables.ExecuteAsync();
        ImportsListBox.ItemsSource = imports;
        CompareTableComboBox.ItemsSource = imports;
    }

    private async Task RefreshOperationsAsync()
    {
        OperationsListBox.ItemsSource = await _listOperations.ExecuteAsync();
    }

    private async void OperationsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OperationsListBox.SelectedItem is not WorkspaceOperation { ResultTableName: not null } operation)
        {
            return;
        }

        _adHocTableName = operation.ResultTableName;
        _pageOffset = 0;
        await RefreshSelectedTableAsync();
    }

    private async void DeleteOperation_Click(object sender, RoutedEventArgs e)
    {
        if (OperationsListBox.SelectedItem is not WorkspaceOperation operation)
        {
            return;
        }
        if (MessageBox.Show(this, "Usunąć wynik tej operacji?", "CSVForge", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await RunUiActionAsync(async cancellationToken =>
        {
            await _deleteOperation.ExecuteAsync(operation.Id, cancellationToken);
            if (string.Equals(_adHocTableName, operation.ResultTableName, StringComparison.OrdinalIgnoreCase))
            {
                _adHocTableName = null;
                DataGrid.ItemsSource = null;
            }
            await RefreshOperationsAsync();
        }, "Wynik usunięty");
    }

    private async void DeleteImport_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedImport is null)
        {
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(this,
            $"Usunąć import '{_selectedImport.DisplayName}' i jego tabelę?",
            "CSVForge", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        Guid importId = _selectedImport.Id;
        await RunUiActionAsync(async cancellationToken =>
        {
            await _deleteImport.ExecuteAsync(importId, cancellationToken);
            _selectedImport = null;
            _adHocTableName = null;
            DataGrid.ItemsSource = null;
            await RefreshImportsAsync();
        }, "Import usunięty");
    }

    private async void RenameImport_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedImport is null)
        {
            return;
        }

        string name = ImportNameTextBox.Text.Trim();
        Guid importId = _selectedImport.Id;
        await RunUiActionAsync(async cancellationToken =>
        {
            await _renameImport.ExecuteAsync(importId, name, cancellationToken);
            await RefreshImportsAsync();
            SelectImport(importId);
        }, "Nazwa zmieniona");
    }

    private void SelectImport(Guid importId)
    {
        foreach (object item in ImportsListBox.Items)
        {
            if (item is CsvImport import && import.Id == importId)
            {
                ImportsListBox.SelectedItem = import;
                break;
            }
        }
    }

    private async Task RefreshSelectedTableAsync()
    {
        string? tableName = _adHocTableName ?? _selectedImport?.TableName;
        if (tableName is null)
        {
            return;
        }

        await RunUiActionAsync(async () =>
        {
            TablePage page = await _browseTable.ExecuteAsync(new BrowseTableRequest(
                tableName,
                PageSize,
                _pageOffset,
                null,
                false,
                FilterTextBox.Text.Trim()));

            string title = _adHocTableName is null ? _selectedImport!.DisplayName : "Wynik operacji";
            TableTitleText.Text = $"{title} ({page.TotalRows} wierszy)";
            DataGrid.ItemsSource = ToRows(page);
            _totalRows = page.TotalRows;
            PreviousPageButton.IsEnabled = _pageOffset > 0;
            NextPageButton.IsEnabled = _pageOffset + page.Rows.Count < page.TotalRows;
            long firstRow = page.Rows.Count == 0 ? 0 : _pageOffset + 1;
            long lastRow = _pageOffset + page.Rows.Count;
            PageStatusText.Text = $"{firstRow}-{lastRow} z {page.TotalRows}";
        }, "Tabela odświeżona");
    }

    private static ObservableCollection<Dictionary<string, string?>> ToRows(TablePage page)
    {
        ObservableCollection<Dictionary<string, string?>> rows = [];
        foreach (IReadOnlyDictionary<string, string?> row in page.Rows)
        {
            rows.Add(page.Columns.ToDictionary(column => column, column => row.TryGetValue(column, out string? value) ? value : null));
        }

        return rows;
    }

    private async Task RunUiActionAsync(Func<Task> action, string successMessage)
    {
        await RunUiActionAsync(_ => action(), successMessage);
    }

    private async Task RunUiActionAsync(Func<CancellationToken, Task> action, string successMessage)
    {
        if (_operationCancellation is not null)
        {
            await action(_operationCancellation.Token);
            return;
        }

        _operationCancellation = new CancellationTokenSource();
        try
        {
            StatusText.Text = "Pracuję...";
            OperationProgressBar.Visibility = Visibility.Visible;
            CancelOperationButton.Visibility = Visibility.Visible;
            await action(_operationCancellation.Token);
            StatusText.Text = successMessage;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Anulowano";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Błąd";
            MessageBox.Show(this, ex.Message, "CSVForge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            OperationProgressBar.Visibility = Visibility.Collapsed;
            CancelOperationButton.Visibility = Visibility.Collapsed;
            _operationCancellation.Dispose();
            _operationCancellation = null;
        }
    }

    private void CancelOperation_Click(object sender, RoutedEventArgs e)
    {
        _operationCancellation?.Cancel();
    }

    private void LoadRecentWorkspaces()
    {
        try
        {
            if (!File.Exists(RecentWorkspacesPath))
            {
                return;
            }

            string[] paths = JsonSerializer.Deserialize<string[]>(File.ReadAllText(RecentWorkspacesPath)) ?? [];
            WorkspacePathTextBox.ItemsSource = paths.Where(File.Exists).ToArray();
            if (WorkspacePathTextBox.Items.Count > 0)
            {
                WorkspacePathTextBox.SelectedIndex = 0;
            }
        }
        catch (JsonException)
        {
            WorkspacePathTextBox.ItemsSource = null;
        }
    }

    private void SaveRecentWorkspace(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string[] existing = (WorkspacePathTextBox.ItemsSource as IEnumerable<string> ?? [])
            .Where(item => !string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase))
            .Prepend(fullPath)
            .Take(10)
            .ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(RecentWorkspacesPath)!);
        File.WriteAllText(RecentWorkspacesPath, JsonSerializer.Serialize(existing));
        WorkspacePathTextBox.ItemsSource = existing;
        WorkspacePathTextBox.Text = fullPath;
    }
}
