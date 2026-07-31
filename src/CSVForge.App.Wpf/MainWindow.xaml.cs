using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Export;
using CSVForge.Application.Operations;
using CSVForge.Application.Csv;
using CSVForge.Application.Tables;
using CSVForge.Application.Sql;
using CSVForge.Domain.Imports;
using CSVForge.Domain.Operations;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.Sqlite;
using Serilog;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace CSVForge.App.Wpf;

public partial class MainWindow : Window
{
    private const int PageSize = 200;
    private static readonly Brush ComparisonAllFilesBrush = CreateFrozenBrush("#DCFCE7");
    private static readonly Brush ComparisonSingleFileBrush = CreateFrozenBrush("#FEE2E2");
    private static readonly Brush[] ComparisonCombinationBrushes =
    [
        CreateFrozenBrush("#DBEAFE"),
        CreateFrozenBrush("#FEF3C7"),
        CreateFrozenBrush("#EDE9FE"),
        CreateFrozenBrush("#FFEDD5"),
        CreateFrozenBrush("#CCFBF1"),
        CreateFrozenBrush("#FCE7F3")
    ];
    private const string CreateNewWorkspaceItem = "+ Utwórz nowy workspace...";
    private static readonly string RecentWorkspacesPath = Path.Combine(AppPaths.DataDirectory, "recent-workspaces.json");
    private static readonly string DefaultWorkspacePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSVForge", "workspace.db");

    private readonly ICreateWorkspaceUseCase _createWorkspace;
    private readonly IOpenWorkspaceUseCase _openWorkspace;
    private readonly IImportCsvUseCase _importCsv;
    private readonly IPreviewCsvUseCase _previewCsv;
    private readonly IListImportedTablesUseCase _listImportedTables;
    private readonly IBrowseTableUseCase _browseTable;
    private readonly IGetColumnValuesUseCase _getColumnValues;
    private readonly IFindDuplicatesUseCase _findDuplicates;
    private readonly ICompareDatasetsUseCase _compareDatasets;
    private readonly IJoinDatasetsUseCase _joinDatasets;
    private readonly IExportTableUseCase _exportTable;
    private readonly ICreateTableFromResultUseCase _createTableFromResult;
    private readonly IListOperationsUseCase _listOperations;
    private readonly IDeleteImportUseCase _deleteImport;
    private readonly IRenameImportUseCase _renameImport;
    private readonly IDeleteOperationUseCase _deleteOperation;
    private readonly IExecuteSqlUseCase _executeSql;
    private readonly IGetSqlSchemaUseCase _getSqlSchema;
    private readonly SqlCompletionService _sqlCompletionService = new();

    private CsvImport? _selectedImport;
    private string? _adHocTableName;
    private int _pageOffset;
    private long _totalRows;
    private string? _sortColumn;
    private bool _sortDescending;
    private CancellationTokenSource? _operationCancellation;
    private string _startupWorkspacePath = DefaultWorkspacePath;
    private string? _currentWorkspacePath;
    private bool _workspaceSelectionReady;
    private bool _ignoreWorkspaceSelection;
    private bool _updatingComparisonFiles;
    private bool _handlingDroppedFiles;
    private int _activeImportCount;
    private SqlSchemaSnapshot _sqlSchema = SqlSchemaSnapshot.Empty;
    private CompletionWindow? _sqlCompletionWindow;
    private string? _sqlResultQuery;
    private readonly List<AdditionalCompareFileRow> _additionalCompareFiles = [];
    private readonly Dictionary<string, IReadOnlyList<string?>> _columnFilters = new(StringComparer.OrdinalIgnoreCase);
    private string? _columnFilterTableName;

    public MainWindow(
        ICreateWorkspaceUseCase createWorkspace,
        IOpenWorkspaceUseCase openWorkspace,
        IImportCsvUseCase importCsv,
        IPreviewCsvUseCase previewCsv,
        IListImportedTablesUseCase listImportedTables,
        IBrowseTableUseCase browseTable,
        IGetColumnValuesUseCase getColumnValues,
        IFindDuplicatesUseCase findDuplicates,
        ICompareDatasetsUseCase compareDatasets,
        IJoinDatasetsUseCase joinDatasets,
        IExportTableUseCase exportTable,
        ICreateTableFromResultUseCase createTableFromResult,
        IListOperationsUseCase listOperations,
        IDeleteImportUseCase deleteImport,
        IRenameImportUseCase renameImport,
        IDeleteOperationUseCase deleteOperation,
        IExecuteSqlUseCase executeSql,
        IGetSqlSchemaUseCase getSqlSchema)
    {
        _createWorkspace = createWorkspace;
        _openWorkspace = openWorkspace;
        _importCsv = importCsv;
        _previewCsv = previewCsv;
        _listImportedTables = listImportedTables;
        _browseTable = browseTable;
        _getColumnValues = getColumnValues;
        _findDuplicates = findDuplicates;
        _compareDatasets = compareDatasets;
        _joinDatasets = joinDatasets;
        _exportTable = exportTable;
        _createTableFromResult = createTableFromResult;
        _listOperations = listOperations;
        _deleteImport = deleteImport;
        _renameImport = renameImport;
        _deleteOperation = deleteOperation;
        _executeSql = executeSql;
        _getSqlSchema = getSqlSchema;

        InitializeComponent();
        InitializeSqlEditor();
        _startupWorkspacePath = LoadRecentWorkspaces();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await RunUiActionAsync(() => OpenWorkspaceAsync(_startupWorkspacePath), "Workspace gotowy");
        _workspaceSelectionReady = true;
    }

    private async void WorkspacePathTextBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_workspaceSelectionReady || _ignoreWorkspaceSelection || WorkspacePathTextBox.SelectedItem is not string selected)
        {
            return;
        }

        if (selected == CreateNewWorkspaceItem)
        {
            RestoreWorkspaceSelection();
            await CreateNewWorkspaceAsync();
            return;
        }

        if (string.Equals(selected, _currentWorkspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? previousPath = _currentWorkspacePath;
        await RunUiActionAsync(() => OpenWorkspaceAsync(selected), "Workspace gotowy");
        if (!string.Equals(_currentWorkspacePath, selected, StringComparison.OrdinalIgnoreCase))
        {
            SelectWorkspace(previousPath ?? _startupWorkspacePath);
        }
    }

    private async Task CreateNewWorkspaceAsync()
    {
        string defaultDirectory = Path.GetDirectoryName(DefaultWorkspacePath)!;
        Directory.CreateDirectory(defaultDirectory);
        SaveFileDialog dialog = new()
        {
            Filter = "CSVForge workspace (*.db)|*.db|All files (*.*)|*.*",
            DefaultExt = ".db",
            AddExtension = true,
            InitialDirectory = defaultDirectory,
            FileName = "workspace.db",
            Title = "Utwórz nowy workspace"
        };

        if (dialog.ShowDialog(this) != true)
        {
            RestoreWorkspaceSelection();
            return;
        }

        await RunUiActionAsync(() => OpenWorkspaceAsync(dialog.FileName), "Nowy workspace gotowy");
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
        _currentWorkspacePath = fullPath;
        SaveRecentWorkspace(fullPath);
        await RefreshImportsAsync();
        await RefreshOperationsAsync();
        await RefreshSqlSchemaAsync();
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
            await ShowImportPreviewAsync(dialog.FileName);
        }
    }

    private void FilesDropArea_DragEnter(object sender, DragEventArgs e)
    {
        UpdateDropFeedback(e);
    }

    private void FilesDropArea_DragOver(object sender, DragEventArgs e)
    {
        UpdateDropFeedback(e);
    }

    private void FilesDropArea_DragLeave(object sender, DragEventArgs e)
    {
        ResetDropFeedback();
    }

    private async void FilesDropArea_Drop(object sender, DragEventArgs e)
    {
        string[] paths = GetDroppedPaths(e.Data);
        ResetDropFeedback();
        e.Handled = true;

        if (_handlingDroppedFiles)
        {
            StatusText.Text = "Poczekaj na zakończenie dodawania plików";
            return;
        }

        string[] csvPaths = paths
            .Where(IsCsvFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] rejectedPaths = paths
            .Where(path => !IsCsvFile(path))
            .ToArray();

        if (csvPaths.Length == 0)
        {
            StatusText.Text = "Nie dodano plików";
            MessageBox.Show(
                this,
                "Upuść co najmniej jeden istniejący plik z rozszerzeniem .csv.",
                "Nieobsługiwany plik",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _handlingDroppedFiles = true;
        try
        {
            foreach (string path in csvPaths)
            {
                StatusText.Text = $"Dodawanie pliku {Path.GetFileName(path)}";
                await ShowImportPreviewAsync(path);
            }

            if (rejectedPaths.Length > 0)
            {
                string rejectedNames = string.Join(
                    Environment.NewLine,
                    rejectedPaths.Take(5).Select(path => $"• {Path.GetFileName(path)}"));
                string more = rejectedPaths.Length > 5
                    ? $"{Environment.NewLine}…oraz {rejectedPaths.Length - 5} kolejnych"
                    : string.Empty;
                MessageBox.Show(
                    this,
                    $"Pominięto pliki, które nie są plikami CSV:{Environment.NewLine}{rejectedNames}{more}",
                    "Niektóre pliki pominięto",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        finally
        {
            _handlingDroppedFiles = false;
        }
    }

    private void UpdateDropFeedback(DragEventArgs e)
    {
        bool canImport = GetDroppedPaths(e.Data).Any(IsCsvFile);
        e.Effects = canImport ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;

        FilesDropArea.SetResourceReference(
            Border.BackgroundProperty,
            canImport ? "AccentSoftBrush" : "SurfaceMutedBrush");
        FilesDropArea.SetResourceReference(
            Border.BorderBrushProperty,
            canImport ? "AccentBrush" : "BorderBrush");
    }

    private void ResetDropFeedback()
    {
        FilesDropArea.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        FilesDropArea.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
    }

    private static string[] GetDroppedPaths(IDataObject data)
    {
        try
        {
            if (!data.GetDataPresent(DataFormats.FileDrop, true))
            {
                return [];
            }

            object? droppedData = data.GetData(DataFormats.FileDrop, true);
            return droppedData switch
            {
                string[] paths => paths,
                IEnumerable<string> paths => paths.ToArray(),
                _ => []
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not read files from a drag-and-drop operation");
            return [];
        }
    }

    private static bool IsCsvFile(string path)
    {
        return File.Exists(path)
            && string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ShowImportPreviewAsync(string filePath)
    {
        ImportPreviewWindow previewWindow = new(_previewCsv, _importCsv, filePath)
        {
            Owner = this
        };
        bool importRegistered = false;
        previewWindow.ProgressChanged += progress =>
        {
            if (!importRegistered)
            {
                importRegistered = true;
                _activeImportCount++;
            }
            ShowImportProgress(progress);
        };

        if (previewWindow.ShowDialog() == true && previewWindow.ImportTask is { } importTask)
        {
            await RefreshImportsAsync();
            CsvImport? provisionalImport = (ImportsListBox.ItemsSource as IEnumerable<CsvImport>)
                ?.FirstOrDefault(item => string.Equals(item.SourcePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (provisionalImport is not null)
            {
                SelectImport(provisionalImport.Id);
                ExpandFilesPanelToFit(provisionalImport.DisplayName);
            }
            _ = TrackBackgroundImportAsync(importTask, importRegistered);
        }
    }

    private void ShowImportProgress(ImportProgress progress)
    {
        ImportWarningBorder.Visibility = Visibility.Visible;
        ImportStatusText.Visibility = Visibility.Visible;
        string remaining = progress.PercentRemaining is { } percent
            ? $", pozostało około {percent}%"
            : string.Empty;
        ImportStatusText.Text = $"Import w tle: {progress.ProcessedRows:N0} wierszy{remaining}";
    }

    private async Task TrackBackgroundImportAsync(Task<ImportResult> importTask, bool importRegistered)
    {
        try
        {
            ImportResult result = await importTask;
            await RefreshImportsAsync();
            SelectImport(result.Import.Id);
            ExpandFilesPanelToFit(result.Import.DisplayName);
            StatusText.Text = $"Zaimportowano {result.Import.RowCount:N0} wierszy";
            await RefreshSqlSchemaAsync();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Import anulowany";
            await RefreshImportsAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Błąd importu";
            await RefreshImportsAsync();
            MessageBox.Show(this, PolishErrorMessage(ex), "CSVForge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (importRegistered)
            {
                _activeImportCount = Math.Max(0, _activeImportCount - 1);
            }
            if (_activeImportCount == 0)
            {
                ImportStatusText.Visibility = Visibility.Collapsed;
                ImportWarningBorder.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async void ImportsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedImport = ImportsListBox.SelectedItem as CsvImport;
        WorkspaceModeTabControl.SelectedItem = BrowseTab;
        _adHocTableName = null;
        _pageOffset = 0;
        _sortColumn = null;
        _sortDescending = false;
        DuplicateColumnComboBox.ItemsSource = _selectedImport?.Columns.Select(column => column.Name).ToArray();
        DuplicateColumnComboBox.SelectedIndex = DuplicateColumnComboBox.Items.Count > 0 ? 0 : -1;
        JoinLeftKeyColumnsComboBox.ItemsSource = _selectedImport?.Columns.Select(column => column.Name).ToArray();
        JoinLeftKeyColumnsComboBox.SelectedIndex = JoinLeftKeyColumnsComboBox.Items.Count > 0 ? 0 : -1;
        UpdateComparisonFiles();
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

    private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = CopyCurrentCellValue();
        }
    }

    private void DataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        DataGridCell? cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell is null)
        {
            return;
        }

        cell.Focus();
        DataGridCellInfo clickedCell = new(cell);
        DataGrid.CurrentCell = clickedCell;
        if (!DataGrid.SelectedCells.Contains(clickedCell))
        {
            DataGrid.SelectedCells.Clear();
            DataGrid.SelectedCells.Add(clickedCell);
        }
    }

    private void CopySelectedCell_Click(object sender, RoutedEventArgs e)
    {
        CopyCurrentCellValue();
    }

    private bool CopyCurrentCellValue()
    {
        List<DataGridCellInfo> selectedCells = DataGrid.SelectedCells
            .Where(cell => cell.IsValid)
            .ToList();
        if (selectedCells.Count == 0 && DataGrid.CurrentCell.IsValid)
        {
            selectedCells.Add(DataGrid.CurrentCell);
        }

        if (selectedCells.Count == 0)
        {
            StatusText.Text = "Najpierw zaznacz co najmniej jedną komórkę";
            return false;
        }

        var cells = selectedCells
            .Select(cell => new
            {
                Cell = cell,
                RowIndex = DataGrid.Items.IndexOf(cell.Item),
                ColumnIndex = cell.Column.DisplayIndex
            })
            .Where(item => item.RowIndex >= 0)
            .OrderBy(item => item.RowIndex)
            .ThenBy(item => item.ColumnIndex)
            .ToArray();
        if (cells.Length == 0)
        {
            StatusText.Text = "Nie udało się odczytać zaznaczenia";
            return false;
        }

        int firstColumn = cells.Min(item => item.ColumnIndex);
        int lastColumn = cells.Max(item => item.ColumnIndex);
        List<string> clipboardRows = [];
        foreach (var rowCells in cells.GroupBy(item => item.RowIndex))
        {
            Dictionary<int, string> valuesByColumn = [];
            foreach (var selected in rowCells)
            {
                if (selected.Cell.Item is not IReadOnlyDictionary<string, string?> row)
                {
                    continue;
                }

                string columnName = selected.Cell.Column.SortMemberPath;
                if (string.IsNullOrWhiteSpace(columnName)
                    || !row.TryGetValue(columnName, out string? value))
                {
                    continue;
                }
                valuesByColumn[selected.ColumnIndex] = FormatClipboardValue(value);
            }

            clipboardRows.Add(string.Join(
                '\t',
                Enumerable.Range(firstColumn, lastColumn - firstColumn + 1)
                    .Select(column => valuesByColumn.GetValueOrDefault(column, string.Empty))));
        }

        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, clipboardRows));
            StatusText.Text = cells.Length == 1
                ? "Skopiowano wartość komórki"
                : $"Skopiowano komórki: {cells.Length:N0}";
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not copy a data grid cell value to the clipboard");
            StatusText.Text = "Nie udało się skopiować wartości";
            MessageBox.Show(
                this,
                "Schowek jest obecnie niedostępny. Spróbuj ponownie.",
                "Kopiowanie nie powiodło się",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return true;
        }
    }

    private static string FormatClipboardValue(string? value)
    {
        string text = value ?? string.Empty;
        return text.IndexOfAny(['\t', '\r', '\n', '"']) < 0
            ? text
            : $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static T? FindVisualParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        e.Row.ClearValue(Control.BackgroundProperty);
        if (e.Row.Item is not IReadOnlyDictionary<string, string?> row)
        {
            return;
        }

        (int Index, bool Present)[] presence = row
            .Where(item => TryGetComparisonFileIndex(item.Key, out _))
            .Select(item =>
            {
                TryGetComparisonFileIndex(item.Key, out int index);
                return (Index: index, Present: string.Equals(item.Value, "✓", StringComparison.Ordinal));
            })
            .OrderBy(item => item.Index)
            .ToArray();
        if (presence.Length < 2)
        {
            return;
        }

        int presentCount = presence.Count(item => item.Present);
        if (presentCount == presence.Length)
        {
            e.Row.Background = ComparisonAllFilesBrush;
            return;
        }
        if (presentCount == 1)
        {
            e.Row.Background = ComparisonSingleFileBrush;
            return;
        }

        int combinationHash = 17;
        foreach ((int index, bool present) in presence)
        {
            combinationHash = unchecked(combinationHash * 31 + (present ? index + 1 : 0));
        }
        e.Row.Background = ComparisonCombinationBrushes[(combinationHash & int.MaxValue) % ComparisonCombinationBrushes.Length];
    }

    private static bool TryGetComparisonFileIndex(string columnName, out int index)
    {
        const string prefix = "plik";
        index = 0;
        return columnName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(columnName.AsSpan(prefix.Length), out index)
            && index > 0;
    }

    private static Brush CreateFrozenBrush(string color)
    {
        SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
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
            ShowValidationMessage("Wybierz plik, w którym chcesz znaleźć duplikaty.");
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
        IReadOnlyList<string> leftKeys = ParseColumns(CompareLeftKeyColumnsComboBox.Text);
        IReadOnlyList<string> rightKeys = RightKeyColumns();
        if (CompareLeftTableComboBox.SelectedItem is not CsvImport leftImport)
        {
            ShowValidationMessage("Wybierz lewy plik z listy plików.");
            return;
        }
        if (CompareTableComboBox.SelectedItem is not CsvImport rightImport)
        {
            ShowValidationMessage("Wybierz prawy plik do porównania.");
            return;
        }
        List<DatasetCompareSource> sources =
        [
            new(leftImport.TableName, leftImport.DisplayName, leftKeys),
            new(rightImport.TableName, rightImport.DisplayName, rightKeys)
        ];
        foreach (AdditionalCompareFileRow row in _additionalCompareFiles)
        {
            if (row.FileComboBox.SelectedItem is not CsvImport import)
            {
                ShowValidationMessage("Wybierz plik w każdym wierszu porównania.");
                return;
            }
            sources.Add(new DatasetCompareSource(
                import.TableName,
                import.DisplayName,
                ParseColumns(row.KeysComboBox.Text)));
        }

        if (sources.Select(source => source.TableName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Count)
        {
            ShowValidationMessage("Każdy porównywany plik musi być inny.");
            return;
        }
        if (leftKeys.Count == 0 || sources.Any(source => source.KeyColumns.Count != leftKeys.Count))
        {
            ShowValidationMessage("Podaj taką samą liczbę kluczy dla każdego pliku.");
            return;
        }

        await RunUiActionAsync(async () =>
        {
            OperationResult result = await _compareDatasets.ExecuteAsync(new DatasetCompareRequest(
                sources,
                SelectedEnum(CompareModeComboBox, DatasetCompareMode.AllWithStatus)));

            _adHocTableName = result.ResultTableName;
            _pageOffset = 0;
            _sortColumn = leftKeys[0];
            _sortDescending = false;
            await RefreshSelectedTableAsync();
            await RefreshOperationsAsync();
        }, "Porównanie gotowe");
    }

    private async void JoinTables_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> rightKeys = ParseColumns(JoinRightKeyColumnsTextBox.Text);
        if (_selectedImport is null)
        {
            ShowValidationMessage("Wybierz lewy plik z listy plików.");
            return;
        }
        if (JoinTableComboBox.SelectedItem is not CsvImport rightImport)
        {
            ShowValidationMessage("Wybierz prawy plik do połączenia.");
            return;
        }
        IReadOnlyList<string> keyColumns = ParseColumns(JoinLeftKeyColumnsComboBox.Text);
        if (keyColumns.Count == 0 || rightKeys.Count != keyColumns.Count)
        {
            ShowValidationMessage("Podaj taką samą liczbę kluczy dla lewego i prawego pliku.");
            return;
        }

        string? missingColumn = rightKeys.FirstOrDefault(key => rightImport.Columns.All(column => !string.Equals(column.Name, key, StringComparison.OrdinalIgnoreCase)));
        if (missingColumn is not null)
        {
            MessageBox.Show(this, $"Prawy plik nie ma kolumny '{missingColumn}'.", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
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
        string? sourceSql = _sqlResultQuery;
        string? tableName = sourceSql is null ? _adHocTableName ?? _selectedImport?.TableName : null;
        if (tableName is null && sourceSql is null)
        {
            MessageBox.Show(this, "Wybierz plik lub wynik do eksportu.", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string[] columns = DataGrid.Columns
            .Select(column => column.SortMemberPath)
            .Where(column => !string.IsNullOrWhiteSpace(column))
            .Cast<string>()
            .ToArray();
        if (columns.Length == 0)
        {
            MessageBox.Show(this, "Tabela nie ma kolumn do eksportu.", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ExportResultWindow options = new(columns) { Owner = this };
        if (options.ShowDialog() != true)
        {
            return;
        }

        if (!options.ExportToCsv)
        {
            await RunUiActionAsync(async cancellationToken =>
            {
                CreateTableFromResultResult result = await _createTableFromResult.ExecuteAsync(
                    new CreateTableFromResultRequest(
                        tableName ?? string.Empty,
                        options.TargetTableName,
                        options.SelectedColumns,
                        FilterTextBox.Text.Trim(),
                        sourceSql,
                        _columnFilters),
                    cancellationToken);
                _adHocTableName = result.TableName;
                _pageOffset = 0;
                await RefreshImportsAsync();
                await RefreshSelectedTableAsync();
                MessageBox.Show(this,
                    $"Utworzono tabelę „{result.TableName}” ({result.RowCount:N0} wierszy).",
                    "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
            }, "Tabela utworzona");
            return;
        }

        SaveFileDialog dialog = new()
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = sourceSql is null ? $"{tableName}.csv" : "wynik_sql.csv",
            Title = "Eksportuj dane do CSV"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await RunUiActionAsync(async cancellationToken =>
        {
            ExportResult result = await _exportTable.ExecuteAsync(
                new ExportTableRequest(
                    tableName ?? string.Empty,
                    dialog.FileName,
                    ';',
                    true,
                    FilterTextBox.Text.Trim(),
                    options.SelectedColumns,
                    sourceSql,
                    _columnFilters),
                cancellationToken);
            MessageBox.Show(this, $"Wyeksportowano {result.ExportedRows} wierszy do:\n{result.FilePath}", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
        }, "Eksport zakończony");
    }

    private async Task RefreshImportsAsync()
    {
        IReadOnlyList<CsvImport> imports = await _listImportedTables.ExecuteAsync();
        ImportsListBox.ItemsSource = imports;
        JoinTableComboBox.ItemsSource = imports;
        UpdateComparisonFiles();
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
            $"Usunąć plik '{_selectedImport.DisplayName}' z workspace?",
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
        }, "Plik usunięty");
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

    private void ListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(listBox, source) is ListBoxItem item)
        {
            item.IsSelected = true;
            return;
        }

        listBox.SelectedItem = null;
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

    private void ExpandFilesPanelToFit(string displayName)
    {
        FormattedText text = new(
            displayName,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(ImportsListBox.FontFamily, ImportsListBox.FontStyle, ImportsListBox.FontWeight, ImportsListBox.FontStretch),
            ImportsListBox.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        double requiredWidth = Math.Ceiling(text.WidthIncludingTrailingWhitespace) + 58;
        if (requiredWidth > FilesPanelColumn.ActualWidth)
        {
            FilesPanelColumn.Width = new GridLength(Math.Min(requiredWidth, FilesPanelColumn.MaxWidth));
        }
    }

    private void UpdateComparisonFiles()
    {
        _updatingComparisonFiles = true;
        try
        {
            CsvImport? previousRight = CompareTableComboBox.SelectedItem as CsvImport;
            CsvImport[] imports = (ImportsListBox.ItemsSource as IEnumerable<CsvImport> ?? []).ToArray();
            CompareLeftTableComboBox.ItemsSource = imports;
            CompareLeftTableComboBox.SelectedItem = _selectedImport is null
                ? null
                : imports.FirstOrDefault(import => import.Id == _selectedImport.Id);
            UpdateLeftComparisonKeys();

            CsvImport[] availableRightFiles = imports
                .Where(import => _selectedImport is null || import.Id != _selectedImport.Id)
                .ToArray();
            CompareTableComboBox.ItemsSource = availableRightFiles;
            CompareTableComboBox.SelectedItem = previousRight is not null
                ? availableRightFiles.FirstOrDefault(import => import.Id == previousRight.Id) ?? availableRightFiles.FirstOrDefault()
                : availableRightFiles.FirstOrDefault();

            foreach (AdditionalCompareFileRow row in _additionalCompareFiles)
            {
                CsvImport? previous = row.FileComboBox.SelectedItem as CsvImport;
                row.FileComboBox.ItemsSource = imports;
                row.FileComboBox.SelectedItem = previous is null
                    ? imports.FirstOrDefault(import => !SelectedComparisonImportIds(row).Contains(import.Id))
                    : imports.FirstOrDefault(import => import.Id == previous.Id);
                UpdateAdditionalComparisonKeys(row);
            }
        }
        finally
        {
            _updatingComparisonFiles = false;
        }
        FilterComparisonFileChoices();
    }

    private void AddCompareFile_Click(object sender, RoutedEventArgs e)
    {
        int number = _additionalCompareFiles.Count + 3;
        Grid grid = new() { Margin = new Thickness(0, 2, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        TextBlock fileLabel = new() { Text = $"Plik {number}", VerticalAlignment = VerticalAlignment.Center };
        ComboBox fileCombo = new()
        {
            Height = 30,
            Margin = new Thickness(10, 0, 10, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = (DataTemplate)FindResource("CompareFileTemplate")
        };
        TextBlock keysLabel = new() { Text = $"Klucze pliku {number}", VerticalAlignment = VerticalAlignment.Center };
        ComboBox keysCombo = new()
        {
            Height = 30,
            Margin = new Thickness(8, 0, 6, 0),
            IsEditable = true,
            ToolTip = "Wybierz jedną kolumnę lub wpisz kilka nazw oddzielonych przecinkami"
        };
        Button removeButton = new() { Content = "Usuń", MinWidth = 56, Padding = new Thickness(7, 4, 7, 4) };

        Grid.SetColumn(fileCombo, 1);
        Grid.SetColumn(keysLabel, 2);
        Grid.SetColumn(keysCombo, 3);
        Grid.SetColumn(removeButton, 4);
        grid.Children.Add(fileLabel);
        grid.Children.Add(fileCombo);
        grid.Children.Add(keysLabel);
        grid.Children.Add(keysCombo);
        grid.Children.Add(removeButton);

        AdditionalCompareFileRow row = new(grid, fileCombo, keysCombo);
        _additionalCompareFiles.Add(row);
        AdditionalCompareFilesPanel.Children.Add(grid);
        fileCombo.SelectionChanged += (_, _) =>
        {
            UpdateAdditionalComparisonKeys(row);
            FilterComparisonFileChoices();
        };
        removeButton.Click += (_, _) =>
        {
            _additionalCompareFiles.Remove(row);
            AdditionalCompareFilesPanel.Children.Remove(grid);
            RenumberAdditionalCompareRows();
            FilterComparisonFileChoices();
        };

        CsvImport[] imports = (ImportsListBox.ItemsSource as IEnumerable<CsvImport> ?? []).ToArray();
        fileCombo.ItemsSource = imports;
        HashSet<Guid> selected = SelectedComparisonImportIds(row);
        fileCombo.SelectedItem = imports.FirstOrDefault(import => !selected.Contains(import.Id));
        UpdateAdditionalComparisonKeys(row);
        FilterComparisonFileChoices();
    }

    private void FilterComparisonFileChoices()
    {
        if (_updatingComparisonFiles)
        {
            return;
        }

        ComboBox[] comboBoxes =
        [
            CompareLeftTableComboBox,
            CompareTableComboBox,
            .. _additionalCompareFiles.Select(row => row.FileComboBox)
        ];
        CsvImport[] imports = (ImportsListBox.ItemsSource as IEnumerable<CsvImport> ?? []).ToArray();
        Guid?[] selections = comboBoxes
            .Select(comboBox => (comboBox.SelectedItem as CsvImport)?.Id)
            .ToArray();

        _updatingComparisonFiles = true;
        try
        {
            for (int index = 0; index < comboBoxes.Length; index++)
            {
                Guid? ownSelection = selections[index];
                HashSet<Guid> selectedElsewhere = selections
                    .Where((selection, selectionIndex) => selectionIndex != index && selection.HasValue)
                    .Select(selection => selection!.Value)
                    .ToHashSet();
                CsvImport[] available = imports
                    .Where(import => !selectedElsewhere.Contains(import.Id))
                    .ToArray();
                comboBoxes[index].ItemsSource = available;
                comboBoxes[index].SelectedItem = ownSelection.HasValue
                    ? available.FirstOrDefault(import => import.Id == ownSelection.Value)
                    : null;
            }
        }
        finally
        {
            _updatingComparisonFiles = false;
        }
    }

    private HashSet<Guid> SelectedComparisonImportIds(AdditionalCompareFileRow? excluded = null)
    {
        HashSet<Guid> selected = [];
        if (CompareLeftTableComboBox.SelectedItem is CsvImport left)
        {
            selected.Add(left.Id);
        }
        if (CompareTableComboBox.SelectedItem is CsvImport right)
        {
            selected.Add(right.Id);
        }
        foreach (AdditionalCompareFileRow row in _additionalCompareFiles.Where(row => row != excluded))
        {
            if (row.FileComboBox.SelectedItem is CsvImport import)
            {
                selected.Add(import.Id);
            }
        }
        return selected;
    }

    private static void UpdateAdditionalComparisonKeys(AdditionalCompareFileRow row)
    {
        if (row.FileComboBox.SelectedItem is not CsvImport import)
        {
            row.KeysComboBox.ItemsSource = null;
            row.KeysComboBox.Text = string.Empty;
            return;
        }
        string[] columns = import.Columns.Select(column => column.Name).ToArray();
        row.KeysComboBox.ItemsSource = columns;
        IReadOnlyList<string> previous = ParseColumns(row.KeysComboBox.Text);
        row.KeysComboBox.Text = previous.Count > 0 && previous.All(key => columns.Contains(key, StringComparer.OrdinalIgnoreCase))
            ? string.Join(",", previous)
            : columns.FirstOrDefault() ?? string.Empty;
    }

    private void RenumberAdditionalCompareRows()
    {
        for (int index = 0; index < _additionalCompareFiles.Count; index++)
        {
            Grid grid = _additionalCompareFiles[index].Container;
            ((TextBlock)grid.Children[0]).Text = $"Plik {index + 3}";
            ((TextBlock)grid.Children[2]).Text = $"Klucze pliku {index + 3}";
        }
    }

    private void CompareLeftTableComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateLeftComparisonKeys();
        if (!_updatingComparisonFiles && CompareLeftTableComboBox.SelectedItem is CsvImport leftImport)
        {
            ImportsListBox.SelectedItem = ImportsListBox.Items.Cast<CsvImport>().FirstOrDefault(import => import.Id == leftImport.Id);
            FilterComparisonFileChoices();
        }
    }

    private async Task RefreshSelectedTableAsync()
    {
        _sqlResultQuery = null;
        string? tableName = _adHocTableName ?? _selectedImport?.TableName;
        if (tableName is null)
        {
            ExportResultButton.Visibility = Visibility.Collapsed;
            return;
        }
        if (!string.Equals(_columnFilterTableName, tableName, StringComparison.OrdinalIgnoreCase))
        {
            _columnFilters.Clear();
            _columnFilterTableName = tableName;
        }

        await RunUiActionAsync(async () =>
        {
            TablePage page = await _browseTable.ExecuteAsync(new BrowseTableRequest(
                tableName,
                PageSize,
                _pageOffset,
                _sortColumn,
                _sortDescending,
                FilterTextBox.Text.Trim(),
                _columnFilters));

            string title = _adHocTableName is null ? _selectedImport!.DisplayName : "Wynik operacji";
            TableTitleText.Text = $"{title} ({page.TotalRows} wierszy)";
            ExportResultButton.Visibility = _adHocTableName is null ? Visibility.Collapsed : Visibility.Visible;
            ShowRows(page.Columns, ToRows(page));
            _totalRows = page.TotalRows;
            PreviousPageButton.IsEnabled = _pageOffset > 0;
            NextPageButton.IsEnabled = _pageOffset + page.Rows.Count < page.TotalRows;
            long firstRow = page.Rows.Count == 0 ? 0 : _pageOffset + 1;
            long lastRow = _pageOffset + page.Rows.Count;
            PageStatusText.Text = $"{firstRow}-{lastRow} z {page.TotalRows}";
        }, "Dane odświeżone");
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

    private void ShowRows(
        IEnumerable<string> columns,
        IEnumerable<Dictionary<string, string?>> rows,
        bool allowSorting = true)
    {
        DataGrid.ItemsSource = null;
        DataGrid.Columns.Clear();
        DataGrid.CanUserSortColumns = allowSorting;

        foreach (string column in columns)
        {
            Grid header = new();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock label = new()
            {
                Text = column,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Button filterButton = new()
            {
                Content = _columnFilters.ContainsKey(column) ? "●" : "▾",
                ToolTip = _columnFilters.ContainsKey(column) ? "Filtr aktywny — kliknij, aby zmienić" : "Filtruj kolumnę",
                MinWidth = 24,
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(6, 0, 0, 0),
                Foreground = _columnFilters.ContainsKey(column) ? Brushes.DodgerBlue : Brushes.SlateGray,
                IsEnabled = allowSorting
            };
            filterButton.Click += async (_, _) => await ShowColumnFilterAsync(column);
            Grid.SetColumn(filterButton, 1);
            header.Children.Add(label);
            header.Children.Add(filterButton);

            DataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                SortMemberPath = column,
                SortDirection = string.Equals(_sortColumn, column, StringComparison.OrdinalIgnoreCase)
                    ? _sortDescending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending
                    : null,
                Binding = new Binding($"[{column}]") { Mode = BindingMode.OneWay },
                Width = DataGridLength.Auto,
                MinWidth = 100,
                MaxWidth = 600
            });
        }

        DataGrid.ItemsSource = rows;
    }

    private async Task ShowColumnFilterAsync(string columnName)
    {
        string? tableName = _adHocTableName ?? _selectedImport?.TableName;
        if (tableName is null || _sqlResultQuery is not null)
        {
            return;
        }

        await RunUiActionAsync(async cancellationToken =>
        {
            IReadOnlyList<ColumnValueOption> values = await _getColumnValues.ExecuteAsync(
                new ColumnValuesRequest(
                    tableName,
                    columnName,
                    FilterTextBox.Text.Trim(),
                    _columnFilters),
                cancellationToken);
            _columnFilters.TryGetValue(columnName, out IReadOnlyList<string?>? selectedValues);
            ColumnFilterWindow dialog = new(columnName, values, selectedValues) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (dialog.ClearFilter || dialog.SelectedValues.Count == dialog.TotalValueCount)
            {
                _columnFilters.Remove(columnName);
            }
            else
            {
                _columnFilters[columnName] = dialog.SelectedValues;
            }
            _pageOffset = 0;
            await RefreshSelectedTableAsync();
        }, "Filtr zastosowany");
    }

    private async void ExecuteSql_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteSqlAsync();
    }

    private void InitializeSqlEditor()
    {
        try
        {
            Uri resourceUri = new("/CSVForge;component/SqlHighlighting.xshd", UriKind.Relative);
            using Stream stream = System.Windows.Application.GetResourceStream(resourceUri).Stream;
            using XmlReader reader = XmlReader.Create(stream);
            SqlQueryTextBox.SyntaxHighlighting = HighlightingLoader.Load(reader, null);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Custom SQL highlighting could not be loaded; using AvalonEdit fallback.");
            SqlQueryTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("SQL");
        }
        SqlQueryTextBox.Options.ConvertTabsToSpaces = true;
        SqlQueryTextBox.Options.IndentationSize = 4;
        SqlQueryTextBox.Options.HighlightCurrentLine = true;
        SqlQueryTextBox.TextArea.AddHandler(
            Keyboard.PreviewKeyDownEvent,
            new KeyEventHandler(SqlEditor_PreviewKeyDown),
            handledEventsToo: true);
        SqlQueryTextBox.TextArea.TextEntered += SqlEditor_TextEntered;
        SqlQueryTextBox.TextArea.TextEntering += SqlEditor_TextEntering;
    }

    private async void SqlEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && _sqlCompletionWindow is not null)
        {
            e.Handled = true;
            _sqlCompletionWindow.CompletionList.RequestInsertion(e);
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            await ExecuteSqlAsync();
            return;
        }

        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            ShowSqlCompletion();
        }
    }

    private void SqlEditor_TextEntered(object? sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length == 1 && (char.IsLetterOrDigit(e.Text[0]) || e.Text[0] is '_' or '.'))
        {
            ShowSqlCompletion();
        }
    }

    private void SqlEditor_TextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || _sqlCompletionWindow is not null)
        {
            return;
        }

        char input = e.Text[0];
        int offset = SqlQueryTextBox.CaretOffset;
        Dictionary<char, char> pairs = new()
        {
            ['('] = ')',
            ['['] = ']',
            ['\''] = '\'',
            ['"'] = '"'
        };

        if (pairs.Values.Contains(input)
            && offset < SqlQueryTextBox.Document.TextLength
            && SqlQueryTextBox.Document.GetCharAt(offset) == input)
        {
            SqlQueryTextBox.CaretOffset++;
            e.Handled = true;
            return;
        }

        if (pairs.TryGetValue(input, out char closing))
        {
            SqlQueryTextBox.Document.Insert(offset, $"{input}{closing}");
            SqlQueryTextBox.CaretOffset = offset + 1;
            e.Handled = true;
        }
    }

    private void ShowSqlCompletion()
    {
        SqlCompletionResult result = _sqlCompletionService.GetSuggestions(
            SqlQueryTextBox.Text,
            SqlQueryTextBox.CaretOffset,
            _sqlSchema);
        if (result.Suggestions.Count == 0)
        {
            _sqlCompletionWindow?.Close();
            return;
        }

        _sqlCompletionWindow?.Close();
        CompletionWindow completionWindow = new(SqlQueryTextBox.TextArea)
        {
            StartOffset = result.ReplacementStart
        };
        completionWindow.CompletionList.IsFiltering = true;
        foreach (SqlSuggestion suggestion in result.Suggestions)
        {
            completionWindow.CompletionList.CompletionData.Add(new SqlCompletionData(suggestion));
        }
        completionWindow.CompletionList.SelectedItem = completionWindow.CompletionList.CompletionData[0];
        completionWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_sqlCompletionWindow, completionWindow))
            {
                _sqlCompletionWindow = null;
            }
        };
        _sqlCompletionWindow = completionWindow;
        completionWindow.Show();
    }

    private async Task RefreshSqlSchemaAsync()
    {
        _sqlSchema = await _getSqlSchema.ExecuteAsync();
    }

    private async Task ExecuteSqlAsync()
    {
        await RunUiActionAsync(async cancellationToken =>
        {
            SqlQueryResult result = await _executeSql.ExecuteAsync(SqlQueryTextBox.Text, cancellationToken);
            ShowRows(
                result.Columns,
                result.Rows.Select(row => row.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)),
                allowSorting: false);

            if (result.Columns.Count > 0)
            {
                _sqlResultQuery = SqlQueryTextBox.Text;
                ExportResultButton.Visibility = Visibility.Visible;
                string suffix = result.WasTruncated ? " — pokazano pierwsze 10 000" : string.Empty;
                SqlStatusText.Text = $"Zwrócono {result.Rows.Count:N0} wierszy{suffix}";
                TableTitleText.Text = $"Wynik SQL ({result.Rows.Count:N0} wierszy)";
            }
            else
            {
                _sqlResultQuery = null;
                ExportResultButton.Visibility = Visibility.Collapsed;
                SqlStatusText.Text = result.AffectedRows >= 0
                    ? $"Polecenie wykonane. Zmienione wiersze: {result.AffectedRows:N0}"
                    : "Polecenie wykonane";
                TableTitleText.Text = "SQL wykonany";
            }

            await RefreshImportsAsync();
            await RefreshOperationsAsync();
            await RefreshSqlSchemaAsync();
        }, "SQL wykonany");
    }

    private void ClearSql_Click(object sender, RoutedEventArgs e)
    {
        SqlQueryTextBox.Clear();
        SqlQueryTextBox.Focus();
        SqlStatusText.Text = "Ctrl+Enter wykonuje zapytanie";
    }

    private sealed class SqlCompletionData(SqlSuggestion suggestion) : ICompletionData
    {
        public System.Windows.Media.ImageSource? Image => null;
        public string Text => suggestion.Text;
        public object Content => $"{KindLabel(suggestion.Kind)}  {suggestion.Text}";
        public object Description => suggestion.Description;
        public double Priority => suggestion.Kind switch
        {
            SqlSuggestionKind.Table or SqlSuggestionKind.View => 4,
            SqlSuggestionKind.Column => 3,
            SqlSuggestionKind.Keyword => 2,
            _ => 1
        };

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
            if (Text.EndsWith("()", StringComparison.Ordinal))
            {
                textArea.Caret.Offset--;
            }
        }

        private static string KindLabel(SqlSuggestionKind kind) => kind switch
        {
            SqlSuggestionKind.Keyword => "SQL",
            SqlSuggestionKind.Function => "ƒ",
            SqlSuggestionKind.Table => "T",
            SqlSuggestionKind.View => "V",
            SqlSuggestionKind.Column => "C",
            _ => "•"
        };
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
        UpdateRightComparisonKeys();
        FilterComparisonFileChoices();
    }

    private void CompareLeftKeyColumnsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRightComparisonKeys();
    }

    private void UpdateLeftComparisonKeys()
    {
        if (CompareLeftTableComboBox.SelectedItem is not CsvImport import)
        {
            CompareLeftKeyColumnsComboBox.ItemsSource = null;
            CompareLeftKeyColumnsComboBox.Text = string.Empty;
            return;
        }

        string previousKeys = CompareLeftKeyColumnsComboBox.Text;
        string[] columns = import.Columns.Select(column => column.Name).ToArray();
        CompareLeftKeyColumnsComboBox.ItemsSource = columns;
        IReadOnlyList<string> parsedPreviousKeys = ParseColumns(previousKeys);
        string selectedKeys = parsedPreviousKeys.Count > 0
            && parsedPreviousKeys.All(key => columns.Contains(key, StringComparer.OrdinalIgnoreCase))
                ? string.Join(",", parsedPreviousKeys)
                : columns.FirstOrDefault() ?? string.Empty;
        CompareLeftKeyColumnsComboBox.Text = selectedKeys;
        UpdateRightComparisonKeys();
    }

    private void UpdateRightComparisonKeys()
    {
        if (CompareTableComboBox.SelectedItem is not CsvImport import)
        {
            RightKeyColumnsTextBox.ItemsSource = null;
            RightKeyColumnsTextBox.Text = string.Empty;
            return;
        }

        string[] columns = import.Columns.Select(column => column.Name).ToArray();
        RightKeyColumnsTextBox.ItemsSource = columns;
        IReadOnlyList<string> leftKeys = ParseColumns(CompareLeftKeyColumnsComboBox.Text);
        string suggestedKeys = leftKeys.Count > 0 && leftKeys.All(key => columns.Contains(key, StringComparer.OrdinalIgnoreCase))
            ? string.Join(",", leftKeys)
            : columns.FirstOrDefault() ?? string.Empty;
        RightKeyColumnsTextBox.SelectedItem = columns.FirstOrDefault(column => string.Equals(column, suggestedKeys, StringComparison.OrdinalIgnoreCase));
        RightKeyColumnsTextBox.Text = suggestedKeys;
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
        IReadOnlyList<string> leftKeys = ParseColumns(JoinLeftKeyColumnsComboBox.Text);
        JoinRightKeyColumnsTextBox.Text = leftKeys.All(key => columns.Contains(key, StringComparer.OrdinalIgnoreCase))
            ? string.Join(",", leftKeys)
            : columns.FirstOrDefault() ?? string.Empty;
    }

    private void JoinLeftKeyColumnsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (JoinTableComboBox.SelectedItem is CsvImport)
        {
            JoinTableComboBox_SelectionChanged(sender, e);
        }
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
        _columnFilters.Clear();
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
        return new ImportRequest(path, displayName, true, null, null, 5000, true);
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

    private void ShowAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            "CSVForge\n\nAutor:\nBorys Patyk\nborys.patyk@gmail.com",
            "O programie CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
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
                SetWorkspaceItems([DefaultWorkspacePath], DefaultWorkspacePath);
                return DefaultWorkspacePath;
            }

            string[] paths = (JsonSerializer.Deserialize<string[]>(File.ReadAllText(RecentWorkspacesPath)) ?? [])
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray();
            string startupPath = paths.FirstOrDefault() ?? DefaultWorkspacePath;
            SetWorkspaceItems(paths.Length == 0 ? [DefaultWorkspacePath] : paths, startupPath);
            return startupPath;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            SetWorkspaceItems([DefaultWorkspacePath], DefaultWorkspacePath);
            return DefaultWorkspacePath;
        }
    }

    private void SaveRecentWorkspace(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string[] existing = (WorkspacePathTextBox.ItemsSource as IEnumerable<string> ?? [])
            .Where(item => item != CreateNewWorkspaceItem)
            .Where(item => !string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase))
            .Prepend(fullPath)
            .Take(10)
            .ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(RecentWorkspacesPath)!);
        File.WriteAllText(RecentWorkspacesPath, JsonSerializer.Serialize(existing));
        SetWorkspaceItems(existing, fullPath);
    }

    private void SetWorkspaceItems(IEnumerable<string> paths, string selectedPath)
    {
        _ignoreWorkspaceSelection = true;
        WorkspacePathTextBox.ItemsSource = paths.Append(CreateNewWorkspaceItem).ToArray();
        WorkspacePathTextBox.SelectedItem = selectedPath;
        _ignoreWorkspaceSelection = false;
    }

    private void SelectWorkspace(string path)
    {
        _ignoreWorkspaceSelection = true;
        WorkspacePathTextBox.SelectedItem = path;
        _ignoreWorkspaceSelection = false;
    }

    private void RestoreWorkspaceSelection()
    {
        SelectWorkspace(_currentWorkspacePath ?? _startupWorkspacePath);
    }

    private sealed record AdditionalCompareFileRow(
        Grid Container,
        ComboBox FileComboBox,
        ComboBox KeysComboBox);
}
