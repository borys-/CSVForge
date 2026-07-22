using System.Collections.ObjectModel;
using System.IO;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Tables;
using CSVForge.Domain.Imports;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace CSVForge.App.Wpf;

public partial class MainWindow : Window
{
    private readonly ICreateWorkspaceUseCase _createWorkspace;
    private readonly IOpenWorkspaceUseCase _openWorkspace;
    private readonly IImportCsvUseCase _importCsv;
    private readonly IListImportedTablesUseCase _listImportedTables;
    private readonly IBrowseTableUseCase _browseTable;

    private CsvImport? _selectedImport;

    public MainWindow(
        ICreateWorkspaceUseCase createWorkspace,
        IOpenWorkspaceUseCase openWorkspace,
        IImportCsvUseCase importCsv,
        IListImportedTablesUseCase listImportedTables,
        IBrowseTableUseCase browseTable)
    {
        _createWorkspace = createWorkspace;
        _openWorkspace = openWorkspace;
        _importCsv = importCsv;
        _listImportedTables = listImportedTables;
        _browseTable = browseTable;

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
        await RefreshSelectedTableAsync();
    }

    private async void RefreshTable_Click(object sender, RoutedEventArgs e)
    {
        await RefreshSelectedTableAsync();
    }

    private async Task RefreshImportsAsync()
    {
        IReadOnlyList<CsvImport> imports = await _listImportedTables.ExecuteAsync();
        ImportsListBox.ItemsSource = imports;
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
        if (_selectedImport is null)
        {
            return;
        }

        await RunUiActionAsync(async () =>
        {
            TablePage page = await _browseTable.ExecuteAsync(new BrowseTableRequest(
                _selectedImport.TableName,
                200,
                0,
                null,
                false,
                FilterTextBox.Text.Trim()));

            TableTitleText.Text = $"{_selectedImport.DisplayName} ({page.TotalRows} wierszy)";
            DataGrid.ItemsSource = ToRows(page);
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
