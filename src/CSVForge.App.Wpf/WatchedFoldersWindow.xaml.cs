using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;

namespace CSVForge.App.Wpf;

public partial class WatchedFoldersWindow : Window
{
    private readonly ObservableCollection<WatchedFolderSetting> _folders;
    public WatchedFoldersWindow(IEnumerable<WatchedFolderSetting> folders)
    {
        InitializeComponent();
        _folders = new(folders.Select(item => new WatchedFolderSetting(item.Path, item.IsEnabled)));
        FoldersGrid.ItemsSource = _folders;
    }
    public IReadOnlyList<WatchedFolderSetting> Folders => _folders;
    public bool RescanRequested { get; private set; }
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new() { Title = "Wybierz folder z plikami CSV", Multiselect = false };
        if (dialog.ShowDialog(this) == true && _folders.All(item => !string.Equals(item.Path, dialog.FolderName, StringComparison.OrdinalIgnoreCase)))
            _folders.Add(new WatchedFolderSetting(dialog.FolderName));
    }
    private void Remove_Click(object sender, RoutedEventArgs e) { if (FoldersGrid.SelectedItem is WatchedFolderSetting item) _folders.Remove(item); }
    private void Rescan_Click(object sender, RoutedEventArgs e) { RescanRequested = true; DialogResult = true; }
    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
