using CSVForge.Application.Tables;
using System.Windows;
using System.Windows.Controls;

namespace CSVForge.App.Wpf;

public partial class ColumnFilterWindow : Window
{
    private readonly List<ValueCheckBox> _valueCheckBoxes = [];

    public ColumnFilterWindow(
        string columnName,
        IReadOnlyList<ColumnValueOption> values,
        IReadOnlyList<string?>? selectedValues)
    {
        InitializeComponent();
        TitleText.Text = $"Filtr: {columnName}";
        HashSet<string?>? selected = selectedValues?.ToHashSet();
        foreach (ColumnValueOption option in values)
        {
            CheckBox checkBox = new()
            {
                Content = $"{DisplayValue(option.Value)}  ({option.Count:N0})",
                IsChecked = selected is null || selected.Contains(option.Value),
                Margin = new Thickness(6, 5, 6, 5)
            };
            _valueCheckBoxes.Add(new ValueCheckBox(option.Value, checkBox));
            ValuesPanel.Children.Add(checkBox);
        }
    }

    public bool ClearFilter { get; private set; }

    public IReadOnlyList<string?> SelectedValues => _valueCheckBoxes
        .Where(item => item.CheckBox.IsChecked == true)
        .Select(item => item.Value)
        .ToArray();

    public int TotalValueCount => _valueCheckBoxes.Count;

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string search = SearchTextBox.Text.Trim();
        foreach (ValueCheckBox item in _valueCheckBoxes)
        {
            item.CheckBox.Visibility = search.Length == 0
                || DisplayValue(item.Value).Contains(search, StringComparison.CurrentCultureIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
    }

    private void SelectVisible_Click(object sender, RoutedEventArgs e) => SetVisible(true);

    private void DeselectVisible_Click(object sender, RoutedEventArgs e) => SetVisible(false);

    private void SetVisible(bool selected)
    {
        foreach (ValueCheckBox item in _valueCheckBoxes.Where(item => item.CheckBox.Visibility == Visibility.Visible))
        {
            item.CheckBox.IsChecked = selected;
        }
    }

    private void ClearFilter_Click(object sender, RoutedEventArgs e)
    {
        ClearFilter = true;
        DialogResult = true;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private static string DisplayValue(string? value) =>
        value is null ? "(NULL)" : value.Length == 0 ? "(Puste)" : value;

    private sealed record ValueCheckBox(string? Value, CheckBox CheckBox);
}
