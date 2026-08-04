using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CSVForge.App.Wpf;

public partial class CommandPaletteWindow : Window
{
    private readonly IReadOnlyList<PaletteCommand> _commands;

    public CommandPaletteWindow(IReadOnlyList<PaletteCommand> commands)
    {
        InitializeComponent();
        _commands = commands;
        Loaded += (_, _) =>
        {
            ApplyFilter();
            SearchTextBox.Focus();
        };
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (CommandsListBox is null) return;
        string query = SearchTextBox?.Text.Trim() ?? string.Empty;
        PaletteCommand[] matches = _commands
            .Where(command => string.IsNullOrEmpty(query)
                || command.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || command.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || (command.Shortcut?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .OrderByDescending(command => command.IsEnabled)
            .ThenBy(command => command.Category)
            .ThenBy(command => command.Name)
            .ToArray();
        CommandsListBox.ItemsSource = matches;
        CommandsListBox.SelectedItem = matches.FirstOrDefault(command => command.IsEnabled);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
        if (e.Key == Key.Enter) { ExecuteSelected(); e.Handled = true; return; }
        if (e.Key is not (Key.Up or Key.Down)) return;

        int direction = e.Key == Key.Down ? 1 : -1;
        int index = CommandsListBox.SelectedIndex;
        for (int i = index + direction; i >= 0 && i < CommandsListBox.Items.Count; i += direction)
        {
            if (CommandsListBox.Items[i] is PaletteCommand { IsEnabled: true })
            {
                CommandsListBox.SelectedIndex = i;
                CommandsListBox.ScrollIntoView(CommandsListBox.SelectedItem);
                break;
            }
        }
        e.Handled = true;
    }

    private void CommandsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ExecuteSelected();

    private void ExecuteSelected()
    {
        if (CommandsListBox.SelectedItem is not PaletteCommand { IsEnabled: true } command) return;
        Close();
        command.Execute();
    }
}
