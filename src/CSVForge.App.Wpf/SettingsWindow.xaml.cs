using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.IO;
using CSVForge.Application.Export;

namespace CSVForge.App.Wpf;

public partial class SettingsWindow : Window
{
    private static readonly DateTimeOffset PreviewTimestamp = new(2026, 8, 5, 14, 7, 0, TimeSpan.Zero);

    public SettingsWindow(string exportNameTemplate)
    {
        InitializeComponent();
        ExportNameTemplateTextBox.Text = string.IsNullOrWhiteSpace(exportNameTemplate)
            ? ExportNameTemplate.Default
            : exportNameTemplate;
        UpdatePreview();
    }

    public string ExportNameTemplateValue => ExportNameTemplateTextBox.Text.Trim();

    private void Template_Changed(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (PreviewText is null) return;
        string file = ExportNameTemplate.ForFile(ExportNameTemplateTextBox.Text, 1234, PreviewTimestamp);
        string table = ExportNameTemplate.ForTable(ExportNameTemplateTextBox.Text, 1234, PreviewTimestamp);
        PreviewText.Text = $"Plik: {file}.csv   •   Tabela: {table}";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ExportNameTemplateValue))
        {
            ExportNameTemplateTextBox.Text = ExportNameTemplate.Default;
        }
        DialogResult = true;
    }

    private void ShowShortcuts_Click(object sender, RoutedEventArgs e)
    {
        ShortcutsWindow shortcuts = new() { Owner = this };
        shortcuts.ShowDialog();
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
}
