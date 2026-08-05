using System.Windows;
using System.Windows.Controls;
using CSVForge.Application.Export;

namespace CSVForge.App.Wpf;

public partial class ExportResultWindow : Window
{
    private readonly long _recordCount;
    private readonly DateTimeOffset _timestamp;

    public ExportResultWindow(IEnumerable<string> columns, long recordCount, string nameTemplate)
    {
        _recordCount = recordCount;
        _timestamp = DateTimeOffset.Now;
        InitializeComponent();
        NameTemplateTextBox.Text = string.IsNullOrWhiteSpace(nameTemplate) ? ExportNameTemplate.Default : nameTemplate;
        UpdateNamePreview();
        foreach (string column in columns)
        {
            ColumnsPanel.Children.Add(new CheckBox
            {
                Content = column,
                IsChecked = true,
                Margin = new Thickness(6, 5, 6, 5)
            });
        }
    }

    public bool ExportToCsv => CsvRadioButton.IsChecked == true;

    public string TargetTableName => TableNameTextBox.Text.Trim();

    public string NameTemplate => NameTemplateTextBox.Text.Trim();

    public string SuggestedFileName => ExportNameTemplate.ForFile(NameTemplate, _recordCount, _timestamp);

    public IReadOnlyList<string> SelectedColumns => ColumnsPanel.Children
        .OfType<CheckBox>()
        .Where(checkBox => checkBox.IsChecked == true)
        .Select(checkBox => checkBox.Content?.ToString() ?? string.Empty)
        .Where(column => column.Length > 0)
        .ToArray();

    private void Destination_Changed(object sender, RoutedEventArgs e)
    {
        if (TableNamePanel is not null)
        {
            TableNamePanel.IsEnabled = TableRadioButton.IsChecked == true;
        }
    }

    private void NameTemplate_Changed(object sender, TextChangedEventArgs e) => UpdateNamePreview();

    private void UpdateNamePreview()
    {
        if (NamePreviewText is null || TableNameTextBox is null) return;
        string fileName = ExportNameTemplate.ForFile(NameTemplateTextBox.Text, _recordCount, _timestamp);
        string tableName = ExportNameTemplate.ForTable(NameTemplateTextBox.Text, _recordCount, _timestamp);
        NamePreviewText.Text = $"Plik: {fileName}.csv   •   Tabela: {tableName}";
        TableNameTextBox.Text = tableName;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAllColumns(true);

    private void ClearAll_Click(object sender, RoutedEventArgs e) => SetAllColumns(false);

    private void SetAllColumns(bool selected)
    {
        foreach (CheckBox checkBox in ColumnsPanel.Children.OfType<CheckBox>())
        {
            checkBox.IsChecked = selected;
        }
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = string.Empty;
        if (SelectedColumns.Count == 0)
        {
            ValidationText.Text = "Wybierz co najmniej jedną kolumnę.";
            return;
        }
        if (!ExportToCsv && string.IsNullOrWhiteSpace(TargetTableName))
        {
            ValidationText.Text = "Podaj nazwę nowej tabeli.";
            return;
        }

        DialogResult = true;
    }
}
