using System.Windows;

namespace CSVForge.App.Wpf;

public partial class RenameImportWindow : Window
{
    public RenameImportWindow(string currentName)
    {
        InitializeComponent();
        NameTextBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    public string ImportName => NameTextBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ImportName))
        {
            MessageBox.Show(this, "Nazwa nie może być pusta.", "CSVForge", MessageBoxButton.OK, MessageBoxImage.Information);
            NameTextBox.Focus();
            return;
        }

        DialogResult = true;
    }
}
