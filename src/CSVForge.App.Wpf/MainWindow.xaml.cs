using System.Collections.ObjectModel;
using System.IO;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Export;
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

    private readonly ICreateWorkspaceUseCase _createWorkspace;
    private readonly IOpenWorkspaceUseCase _openWorkspace;
    private readonly IImportCsvUseCase _importCsv;
    private readonly IListImportedTablesUseCase _listImportedTables;
    private readonly IBrowseTableUseCase _browseTable;
    private readonly IFindDuplicatesUseCase _findDuplicates;
    private readonly ICompareDatasetsUseCase _compareDatasets;
    private readonly IJoinDatasetsUseCase _joinDatasets;
    private readonly IExportTableUseCase _exportTable;

    private CsvImport? _selectedImport;
    private string? _adHocTableName;
    private int _pageOffset;
    private long _totalRows;

    public MainWindow(
        ICreateWorkspaceUseCase createWorkspace,
        IOpenWorkspaceUseCase openWorkspace,
        IImportCsvUseCase importCsv,
        IListImportedTablesUseCase listImportedTables,
        IBrowseTableUseCase browseTable,
        IFindDuplicatesUseCase findDuplicates,
        ICompareDatasetsUseCase compareDatasets,
        IJoinDatasetsUseCase joinDatasets,
        IExportTableUseCase exportTable)
    {
        _createWorkspace = createWorkspace;
        _openWorkspace = openWorkspace;
        _importCsv = importCsv;
        _listImportedTables = listImportedTables;
        _browseTable = browseTable;
        _findDuplicates = findDuplicates;
        _compareDatasets = compareDatasets;
        _joinDatasets = joinDatasets;
        _exportTable = exportTable;

        InitializeComponent();
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
            await RefreshImportsAsync();
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
        await RunUiActionAsync(async () =>
        {
            string path = CsvPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this, "Wybierz plik CSV do importu.", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string displayName = Path.GetFileNameWithoutExtension(path);
            ImportResult result = await _importCsv.ExecuteAsync(new ImportRequest(path, displayName, true, null, null));
            await RefreshImportsAsync();
            SelectImport(result.Import.Id);
        }, "Import zakończony");
    }

    private async void ImportsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedImport = ImportsListBox.SelectedItem as CsvImport;
        _adHocTableName = null;
        _pageOffset = 0;
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

        await RunUiActionAsync(async () =>
        {
            ExportResult result = await _exportTable.ExecuteAsync(new ExportTableRequest(tableName, dialog.FileName, delimiter, true));
            MessageBox.Show(this, $"Wyeksportowano {result.ExportedRows} wierszy do:\n{result.FilePath}", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
        }, "Eksport zakończony");
    }

    private async Task RefreshImportsAsync()
    {
        IReadOnlyList<CsvImport> imports = await _listImportedTables.ExecuteAsync();
        ImportsListBox.ItemsSource = imports;
        CompareTableComboBox.ItemsSource = imports;
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
        try
        {
            StatusText.Text = "Pracuję...";
            await action();
            StatusText.Text = successMessage;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Błąd";
            MessageBox.Show(this, ex.Message, "CSVForge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
