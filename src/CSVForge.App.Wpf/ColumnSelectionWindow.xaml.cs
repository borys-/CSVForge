using System.Windows;
using System.Windows.Controls;

namespace CSVForge.App.Wpf;

public partial class ColumnSelectionWindow : Window
{
    public ColumnSelectionWindow(
        string title,
        IEnumerable<string> availableColumns,
        IEnumerable<string> selectedColumns)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        HashSet<string> selected = selectedColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string column in availableColumns)
        {
            CheckBox checkBox = new()
            {
                Content = column,
                IsChecked = selected.Contains(column),
                Margin = new Thickness(6, 5, 6, 5)
            };
            checkBox.Checked += Selection_Changed;
            checkBox.Unchecked += Selection_Changed;
            ColumnsPanel.Children.Add(checkBox);
        }
        UpdateStatus();
    }

    public IReadOnlyList<string> SelectedColumns => ColumnsPanel.Children
        .OfType<CheckBox>()
        .Where(checkBox => checkBox.IsChecked == true)
        .Select(checkBox => checkBox.Content?.ToString() ?? string.Empty)
        .Where(column => column.Length > 0)
        .ToArray();

    private void Selection_Changed(object sender, RoutedEventArgs e) => UpdateStatus();
    private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);
    private void ClearAll_Click(object sender, RoutedEventArgs e) => SetAll(false);

    private void SetAll(bool selected)
    {
        foreach (CheckBox checkBox in ColumnsPanel.Children.OfType<CheckBox>()) checkBox.IsChecked = selected;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (SelectionStatusText is not null) SelectionStatusText.Text = $"Wybrano: {SelectedColumns.Count}";
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
