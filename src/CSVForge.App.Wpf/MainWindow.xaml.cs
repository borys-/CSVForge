using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Data.Sqlite;

namespace CSVForge.App.Wpf;

public partial class MainWindow : Window
{
    private const int PageSize = 200;
    private static readonly string RecentWorkspacesPath = Path.Combine(AppPaths.DataDirectory, "recent-workspaces.json");
    private static readonly string DefaultWorkspacePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSVForge", "workspace.db");

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
    private string? _sortColumn;
    private bool _sortDescending;
    private CancellationTokenSource? _operationCancellation;
    private string _startupWorkspacePath = DefaultWorkspacePath;

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
        _startupWorkspacePath = LoadRecentWorkspaces();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await RunUiActionAsync(() => OpenWorkspaceAsync(_startupWorkspacePath), "Workspace gotowy");
    }

    private async void OpenWorkspace_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            string path = WorkspacePathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                path = DefaultWorkspacePath;
            }
            await OpenWorkspaceAsync(path);
        }, "Workspace gotowy");
    }

    private async Task OpenWorkspaceAsync(string path)
    {
        string fullPath = Path.GetFullPath(path);
        WorkspacePathTextBox.Text = fullPath;

        if (File.Exists(fullPath))
        {
            await _openWorkspace.ExecuteAsync(fullPath);
        }
        else
        {
            await _createWorkspace.ExecuteAsync(fullPath);
        }

        WorkspaceStatusText.Text = fullPath;
        SaveRecentWorkspace(fullPath);
        await RefreshImportsAsync();
        await RefreshOperationsAsync();
    }

    private async void ChooseCsv_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Wybierz plik CSV"
        };

        if (dialog.ShowDialog(this) == true)
        {
            CsvPathTextBox.Text = dialog.FileName;
            ImportPreviewWindow previewWindow = new(_previewCsv, _importCsv, dialog.FileName)
            {
                Owner = this
            };

            if (previewWindow.ShowDialog() == true && previewWindow.ImportedResult is { } result)
            {
                await RefreshImportsAsync();
                SelectImport(result.Import.Id);
                StatusText.Text = $"Zaimportowano {result.Import.RowCount} wierszy";
            }
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
            ImportResult result = await _importCsv.ExecuteAsync(CreateImportRequest(path, displayName), progress, cancellationToken);
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
            CsvPreview preview = await _previewCsv.ExecuteAsync(CreateImportRequest(path, Path.GetFileNameWithoutExtension(path)), cancellationToken);
            List<Dictionary<string, string?>> rows = preview.Rows.Select(row => preview.Columns
                .Select((column, index) => new { column.Name, Value = index < row.Count ? row[index] : null })
                .ToDictionary(item => item.Name, item => item.Value)).ToList();
            ShowRows(preview.Columns.Select(column => column.Name), rows);
            TableTitleText.Text = $"Podgląd: {Path.GetFileName(path)} ({preview.Rows.Count} wierszy)";
            PageStatusText.Text = preview.Errors.Count == 0 ? "CSV poprawny" : $"Błędy: {preview.Errors.Count}";
        }, "Podgląd gotowy");
    }

    private async void ImportsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedImport = ImportsListBox.SelectedItem as CsvImport;
        _adHocTableName = null;
        _pageOffset = 0;
        _sortColumn = null;
        _sortDescending = false;
        DuplicateColumnComboBox.ItemsSource = _selectedImport?.Columns.Select(column => column.Name).ToArray();
        DuplicateColumnComboBox.SelectedIndex = DuplicateColumnComboBox.Items.Count > 0 ? 0 : -1;
        LeftOutputColumnsTextBox.Text = _selectedImport is null ? string.Empty : string.Join(",", _selectedImport.Columns.Select(column => column.Name));
        await RefreshSelectedTableAsync();
    }

    private async void RefreshTable_Click(object sender, RoutedEventArgs e)
    {
        _pageOffset = 0;
        await RefreshSelectedTableAsync();
    }

    private async void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        string column = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(column))
        {
            column = e.Column.Header?.ToString() ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(column))
        {
            return;
        }

        _sortDescending = string.Equals(_sortColumn, column, StringComparison.OrdinalIgnoreCase) && !_sortDescending;
        _sortColumn = column;
        _pageOffset = 0;
        foreach (DataGridColumn gridColumn in DataGrid.Columns)
        {
            gridColumn.SortDirection = null;
        }
        e.Column.SortDirection = _sortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;
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
        IReadOnlyList<string> keyColumns = SelectedKeyColumns();
        if (_selectedImport is null)
        {
            ShowValidationMessage("Wybierz tabelę, w której chcesz znaleźć duplikaty.");
            return;
        }
        if (keyColumns.Count == 0)
        {
            ShowValidationMessage("Wybierz co najmniej jedną kolumnę klucza.");
            return;
        }

        await RunUiActionAsync(async () =>
        {
            OperationResult result = await _findDuplicates.ExecuteAsync(new DuplicateSearchRequest(
                _selectedImport.TableName,
                keyColumns,
                SelectedEnum(DuplicateModeComboBox, DuplicateSearchMode.AllDuplicateRows),
                IgnoreEmptyKeysCheckBox.IsChecked == true));

            _adHocTableName = result.ResultTableName;
            _pageOffset = 0;
            await RefreshSelectedTableAsync();
            await RefreshOperationsAsync();
        }, "Duplikaty gotowe");
    }

    private async void CompareTables_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> rightKeys = RightKeyColumns();
        if (_selectedImport is null)
        {
            ShowValidationMessage("Wybierz tabelę lewą z listy tabel.");
            return;
        }
        if (CompareTableComboBox.SelectedItem is not CsvImport rightImport)
        {
            ShowValidationMessage("Wybierz tabelę prawą do porównania.");
            return;
        }
        IReadOnlyList<string> keyColumns = SelectedKeyColumns();
        if (keyColumns.Count == 0 || rightKeys.Count != keyColumns.Count)
        {
            ShowValidationMessage("Podaj taką samą liczbę kluczy dla lewej i prawej tabeli.");
            return;
        }

        await RunUiActionAsync(async () =>
        {
            OperationResult result = await _compareDatasets.ExecuteAsync(new DatasetCompareRequest(
                _selectedImport.TableName,
                rightImport.TableName,
                rightKeys,
                keyColumns,
                SelectedEnum(CompareModeComboBox, DatasetCompareMode.AllWithStatus)));

            _adHocTableName = result.ResultTableName;
            _pageOffset = 0;
            await RefreshSelectedTableAsync();
            await RefreshOperationsAsync();
        }, "Porównanie gotowe");
    }

    private async void JoinTables_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> rightKeys = ParseColumns(JoinRightKeyColumnsTextBox.Text);
        if (_selectedImport is null)
        {
            ShowValidationMessage("Wybierz tabelę lewą z listy tabel.");
            return;
        }
        if (JoinTableComboBox.SelectedItem is not CsvImport rightImport)
        {
            ShowValidationMessage("Wybierz tabelę prawą do połączenia.");
            return;
        }
        IReadOnlyList<string> keyColumns = SelectedKeyColumns();
        if (keyColumns.Count == 0 || rightKeys.Count != keyColumns.Count)
        {
            ShowValidationMessage("Podaj taką samą liczbę kluczy dla lewej i prawej tabeli.");
            return;
        }

        string? missingColumn = rightKeys.FirstOrDefault(key => rightImport.Columns.All(column => !string.Equals(column.Name, key, StringComparison.OrdinalIgnoreCase)));
        if (missingColumn is not null)
        {
            MessageBox.Show(this, $"Tabela po prawej stronie nie ma kolumny '{missingColumn}'.", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await RunUiActionAsync(async () =>
        {
            OperationResult result = await _joinDatasets.ExecuteAsync(new DatasetJoinRequest(
                _selectedImport.TableName,
                rightImport.TableName,
                keyColumns,
                rightKeys,
                ParseColumns(LeftOutputColumnsTextBox.Text),
                ParseColumns(RightOutputColumnsTextBox.Text),
                SelectedEnum(JoinTypeComboBox, DatasetJoinType.Left)));

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
            ExportResult result = await _exportTable.ExecuteAsync(new ExportTableRequest(tableName, dialog.FileName, delimiter, true, FilterTextBox.Text.Trim()), cancellationToken);
            MessageBox.Show(this, $"Wyeksportowano {result.ExportedRows} wierszy do:\n{result.FilePath}", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
        }, "Eksport zakończony");
    }

    private async Task RefreshImportsAsync()
    {
        IReadOnlyList<CsvImport> imports = await _listImportedTables.ExecuteAsync();
        ImportsListBox.ItemsSource = imports;
        CompareTableComboBox.ItemsSource = imports;
        JoinTableComboBox.ItemsSource = imports;
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

        RenameImportWindow dialog = new(_selectedImport.DisplayName) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string name = dialog.ImportName;
        Guid importId = _selectedImport.Id;
        await RunUiActionAsync(async cancellationToken =>
        {
            await _renameImport.ExecuteAsync(importId, name, cancellationToken);
            await RefreshImportsAsync();
            SelectImport(importId);
        }, "Nazwa zmieniona");
    }

    private void ImportContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu { PlacementTarget: FrameworkElement { DataContext: CsvImport import } })
        {
            ImportsListBox.SelectedItem = import;
        }
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
                _sortColumn,
                _sortDescending,
                FilterTextBox.Text.Trim()));

            string title = _adHocTableName is null ? _selectedImport!.DisplayName : "Wynik operacji";
            TableTitleText.Text = $"{title} ({page.TotalRows} wierszy)";
            ShowRows(page.Columns, ToRows(page));
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

    private void ShowRows(IEnumerable<string> columns, IEnumerable<Dictionary<string, string?>> rows)
    {
        DataGrid.ItemsSource = null;
        DataGrid.Columns.Clear();

        foreach (string column in columns)
        {
            DataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = column,
                SortMemberPath = column,
                Binding = new Binding($"[{column}]") { Mode = BindingMode.OneWay },
                Width = DataGridLength.Auto,
                MinWidth = 100,
                MaxWidth = 600
            });
        }

        DataGrid.ItemsSource = rows;
    }

    private IReadOnlyList<string> SelectedKeyColumns()
    {
        return DuplicateColumnComboBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private IReadOnlyList<string> RightKeyColumns() => ParseColumns(RightKeyColumnsTextBox.Text);

    private static IReadOnlyList<string> ParseColumns(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void CompareTableComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CompareTableComboBox.SelectedItem is not CsvImport import)
        {
            RightKeyColumnsTextBox.Text = string.Empty;
            return;
        }

        string[] columns = import.Columns.Select(column => column.Name).ToArray();
        IReadOnlyList<string> leftKeys = SelectedKeyColumns();
        RightKeyColumnsTextBox.Text = leftKeys.All(key => columns.Contains(key, StringComparer.OrdinalIgnoreCase))
            ? string.Join(",", leftKeys)
            : columns.FirstOrDefault() ?? string.Empty;
    }

    private void JoinTableComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (JoinTableComboBox.SelectedItem is not CsvImport import)
        {
            JoinRightKeyColumnsTextBox.Text = string.Empty;
            RightOutputColumnsTextBox.Text = string.Empty;
            return;
        }

        string[] columns = import.Columns.Select(column => column.Name).ToArray();
        RightOutputColumnsTextBox.Text = string.Join(",", columns);
        IReadOnlyList<string> leftKeys = SelectedKeyColumns();
        JoinRightKeyColumnsTextBox.Text = leftKeys.All(key => columns.Contains(key, StringComparer.OrdinalIgnoreCase))
            ? string.Join(",", leftKeys)
            : columns.FirstOrDefault() ?? string.Empty;
    }

    private async void FilterTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        _pageOffset = 0;
        await RefreshSelectedTableAsync();
    }

    private async void ClearFilter_Click(object sender, RoutedEventArgs e)
    {
        FilterTextBox.Clear();
        _pageOffset = 0;
        await RefreshSelectedTableAsync();
        FilterTextBox.Focus();
    }

    private static T SelectedEnum<T>(ComboBox comboBox, T fallback) where T : struct, Enum
    {
        return comboBox.SelectedItem is ComboBoxItem { Tag: string value } && Enum.TryParse(value, true, out T parsed)
            ? parsed
            : fallback;
    }

    private void ShowValidationMessage(string message)
    {
        MessageBox.Show(this, message, "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private ImportRequest CreateImportRequest(string path, string displayName)
    {
        return new ImportRequest(path, displayName, true, null, null, 500, true);
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
            MessageBox.Show(this, PolishErrorMessage(ex), "CSVForge", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppPaths.LogsDirectory);
        Process.Start(new ProcessStartInfo(AppPaths.LogsDirectory) { UseShellExecute = true });
    }

    private void ShowHelp_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            "1. Utwórz lub otwórz workspace.\n2. Wybierz CSV i sprawdź podgląd.\n3. Zaimportuj dane.\n4. Wybierz tabelę, klucze oraz operację.\n5. Wyniki możesz filtrować, przeglądać stronami i eksportować.",
            "CSVForge - pomoc", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string PolishErrorMessage(Exception exception)
    {
        return exception switch
        {
            FileNotFoundException => "Nie znaleziono wskazanego pliku.",
            DirectoryNotFoundException => "Nie znaleziono wskazanego katalogu.",
            UnauthorizedAccessException => "Brak uprawnień do pliku lub katalogu.",
            SqliteException { SqliteErrorCode: 5 or 6 } => "Workspace jest używany przez inną operację. Spróbuj ponownie za chwilę.",
            IOException => "Nie udało się odczytać lub zapisać pliku. Sprawdź dostępne miejsce i czy plik nie jest zablokowany.",
            ArgumentException => $"Nieprawidłowe dane: {exception.Message}",
            InvalidOperationException => exception.Message,
            _ => $"Wystąpił nieoczekiwany błąd: {exception.Message}"
        };
    }

    private string LoadRecentWorkspaces()
    {
        try
        {
            if (!File.Exists(RecentWorkspacesPath))
            {
                WorkspacePathTextBox.ItemsSource = new[] { DefaultWorkspacePath };
                WorkspacePathTextBox.Text = DefaultWorkspacePath;
                return DefaultWorkspacePath;
            }

            string[] paths = (JsonSerializer.Deserialize<string[]>(File.ReadAllText(RecentWorkspacesPath)) ?? [])
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray();
            string startupPath = paths.FirstOrDefault() ?? DefaultWorkspacePath;
            WorkspacePathTextBox.ItemsSource = paths.Length == 0 ? new[] { DefaultWorkspacePath } : paths;
            WorkspacePathTextBox.Text = startupPath;
            return startupPath;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            WorkspacePathTextBox.ItemsSource = new[] { DefaultWorkspacePath };
            WorkspacePathTextBox.Text = DefaultWorkspacePath;
            return DefaultWorkspacePath;
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
