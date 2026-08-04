using System.Windows;
using System.Windows.Input;

namespace CSVForge.App.Wpf;

public partial class ShortcutsWindow : Window
{
    public IReadOnlyList<ShortcutItem> Shortcuts { get; } =
    [
        new("Ctrl+K", "Otwórz paletę poleceń", "Globalne"),
        new("F1", "Pokaż wszystkie skróty", "Globalne"),
        new("Ctrl+N", "Utwórz nowy workspace", "Workspace"),
        new("Ctrl+Shift+O", "Optymalizuj workspace", "Workspace"),
        new("Ctrl+O", "Dodaj plik CSV", "Pliki"),
        new("Ctrl+E", "Eksportuj bieżący wynik", "Dane"),
        new("Ctrl+R", "Odśwież wybraną tabelę", "Dane"),
        new("Ctrl+1", "Przejdź do Przeglądaj", "Widok"),
        new("Ctrl+2", "Przejdź do Duplikaty", "Widok"),
        new("Ctrl+3", "Przejdź do Porównaj", "Widok"),
        new("Ctrl+4", "Przejdź do Połącz", "Widok"),
        new("Ctrl+5", "Przejdź do SQL", "Widok"),
        new("Ctrl+Enter", "Wykonaj zapytanie", "Edytor SQL"),
        new("Ctrl+Space", "Otwórz podpowiedzi", "Edytor SQL"),
        new("Ctrl+C", "Kopiuj zaznaczone komórki", "Tabela"),
        new("Esc", "Zamknij paletę lub okno skrótów", "Okna")
    ];

    public ShortcutsWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
}

public sealed record ShortcutItem(string Keys, string Description, string Category);
