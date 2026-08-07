using System.Windows;
using System.Windows.Controls;

namespace CSVForge.App.Wpf;

public partial class ExportResultWindow : Window
{
    public ExportResultWindow(
        IEnumerable<string> columns,
        string suggestedTableName,
        IReadOnlySet<string>? initiallySelectedColumns = null)
    {
        InitializeComponent();
        TableNameTextBox.Text = suggestedTableName;
        foreach (string column in columns)
        {
            ColumnsPanel.Children.Add(new CheckBox
            {
                Content = column,
                IsChecked = initiallySelectedColumns is not { Count: > 0 }
                    || initiallySelectedColumns.Contains(column),
                Margin = new Thickness(6, 5, 6, 5)
            });
        }
    }

    public bool ExportToCsv => CsvRadioButton.IsChecked == true;

    public string TargetTableName => TableNameTextBox.Text.Trim();

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
