using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Csv;
using CSVForge.Domain.Imports;

namespace CSVForge.App.Wpf;

public partial class ImportPreviewWindow : Window
{
    private readonly IPreviewCsvUseCase _previewCsv;
    private readonly IImportCsvUseCase _importCsv;
    private readonly string _filePath;
    private readonly ObservableCollection<ColumnSetting> _columnSettings = [];
    private bool _isLoaded;

    public ImportPreviewWindow(IPreviewCsvUseCase previewCsv, IImportCsvUseCase importCsv, string filePath)
    {
        _previewCsv = previewCsv;
        _importCsv = importCsv;
        _filePath = filePath;

        InitializeComponent();
        FileNameText.Text = filePath;
        DisplayNameTextBox.Text = Path.GetFileNameWithoutExtension(filePath);
        ColumnTypeDataGridColumn.ItemsSource = new[]
        {
            new ColumnTypeOption("Tekst", CsvColumnDataType.Text),
            new ColumnTypeOption("Liczba całkowita", CsvColumnDataType.Integer),
            new ColumnTypeOption("Liczba dziesiętna", CsvColumnDataType.Decimal),
            new ColumnTypeOption("Data", CsvColumnDataType.Date),
            new ColumnTypeOption("Tak / nie", CsvColumnDataType.Boolean)
        };
        ColumnSettingsDataGrid.ItemsSource = _columnSettings;
        Loaded += ImportPreviewWindow_Loaded;
    }

    public ImportResult? ImportedResult { get; private set; }
    public Task<ImportResult>? ImportTask { get; private set; }
    public event Action<ImportProgress>? ProgressChanged;

    private async void ImportPreviewWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        await RefreshPreviewAsync();
    }

    private async void PreviewSetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoaded)
        {
            await RefreshPreviewAsync();
        }
    }

    private async Task RefreshPreviewAsync()
    {
        try
        {
            SetBusy(true, "Wczytywanie podglądu...");
            CsvPreview preview = await _previewCsv.ExecuteAsync(CreateRequest());
            ShowPreview(preview);
            PreviewStatusText.Text = preview.Errors.Count == 0
                ? $"{preview.Rows.Count} wierszy"
                : $"{preview.Rows.Count} wierszy, błędy: {preview.Errors.Count}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "CSVForge", MessageBoxButton.OK, MessageBoxImage.Error);
            PreviewStatusText.Text = "Nie udało się wczytać pliku";
        }
        finally
        {
            SetBusy(false, string.Empty);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            MessageBox.Show(this, "Podaj nazwę pliku w workspace.", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
            DisplayNameTextBox.Focus();
            return;
        }

        SetBusy(true, "Importowanie pierwszej partii...");
        Progress<ImportProgress> progress = new(ImportProgressChanged);
        ImportTask = _importCsv.ExecuteAsync(CreateRequest(), progress);
        _ = ObserveImportAsync(ImportTask);
    }

    private void ImportProgressChanged(ImportProgress progress)
    {
        OperationStatusText.Text = $"Zaimportowano: {progress.ProcessedRows:N0} wierszy";
        ProgressChanged?.Invoke(progress);
        if (IsVisible && progress.CurrentStep is "Batch committed" or "Import completed")
        {
            DialogResult = true;
        }
    }

    private async Task ObserveImportAsync(Task<ImportResult> importTask)
    {
        try
        {
            ImportedResult = await importTask;
            if (IsVisible)
            {
                DialogResult = true;
            }
        }
        catch (Exception ex)
        {
            if (IsVisible)
            {
                MessageBox.Show(this, ex.Message, "CSVForge", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            if (IsVisible)
            {
                SetBusy(false, string.Empty);
            }
        }
    }

    private ImportRequest CreateRequest()
    {
        ColumnSettingsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ColumnSettingsDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        string mode = HeaderModeComboBox.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : "Auto";
        IReadOnlyList<CsvColumnMapping>? mappings = _columnSettings.Count == 0
            ? null
            : _columnSettings.Select(setting => new CsvColumnMapping(
                setting.SourceIndex,
                setting.Name,
                setting.DataType,
                setting.Include)).ToArray();
        return new ImportRequest(_filePath, DisplayNameTextBox.Text.Trim(), mode != "No", null, null, 5000, mode == "Auto", mappings);
    }

    private void SelectAllColumns_Click(object sender, RoutedEventArgs e) => SetAllColumnsIncluded(true);

    private void DeselectAllColumns_Click(object sender, RoutedEventArgs e) => SetAllColumnsIncluded(false);

    private void SetAllColumnsIncluded(bool included)
    {
        ColumnSettingsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ColumnSettingsDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        foreach (ColumnSetting setting in _columnSettings)
        {
            setting.Include = included;
        }
        ColumnSettingsDataGrid.Items.Refresh();
    }

    private void ShowPreview(CsvPreview preview)
    {
        PreviewDataGrid.ItemsSource = null;
        PreviewDataGrid.Columns.Clear();

        foreach (CsvColumn column in preview.Columns)
        {
            PreviewDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = column.Name,
                Binding = new Binding($"[{column.Name}]") { Mode = BindingMode.OneWay },
                Width = DataGridLength.Auto,
                MinWidth = 100,
                MaxWidth = 500
            });
        }

        ObservableCollection<Dictionary<string, string?>> rows = [];
        foreach (IReadOnlyList<string> sourceRow in preview.Rows)
        {
            Dictionary<string, string?> row = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < preview.Columns.Count; index++)
            {
                row[preview.Columns[index].Name] = index < sourceRow.Count ? sourceRow[index] : null;
            }
            rows.Add(row);
        }

        PreviewDataGrid.ItemsSource = rows;

        _columnSettings.Clear();
        foreach (CsvColumn column in preview.Columns)
        {
            _columnSettings.Add(new ColumnSetting
            {
                SourceIndex = column.Index,
                SourceName = column.OriginalName,
                Name = column.Name,
                DataType = CsvColumnDataType.Text,
                Include = true
            });
        }
    }

    private void SetBusy(bool busy, string status)
    {
        ImportButton.IsEnabled = !busy;
        HeaderModeComboBox.IsEnabled = !busy;
        ImportProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        OperationStatusText.Text = status;
    }

    private sealed class ColumnSetting
    {
        public int SourceIndex { get; init; }
        public string SourceName { get; init; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public CsvColumnDataType DataType { get; set; }
        public bool Include { get; set; }
    }

    private sealed record ColumnTypeOption(string Label, CsvColumnDataType Value);
}
