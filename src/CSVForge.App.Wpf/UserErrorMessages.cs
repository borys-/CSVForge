using System.IO;
using Microsoft.Data.Sqlite;

namespace CSVForge.App.Wpf;

internal static class UserErrorMessages
{
    public static string From(Exception exception) => exception switch
    {
        OperationCanceledException => "Operacja została anulowana.",
        FileNotFoundException => "Nie znaleziono wskazanego pliku.",
        DirectoryNotFoundException => "Nie znaleziono wskazanego katalogu.",
        UnauthorizedAccessException => "Brak uprawnień do pliku lub katalogu.",
        SqliteException { SqliteErrorCode: 5 or 6 } => "Workspace jest zablokowany przez inny proces. Zamknij go lub spróbuj ponownie później.",
        SqliteException { SqliteErrorCode: 13 } => "Brak miejsca na dysku podczas zapisu workspace.",
        SqliteException => "Operacja bazy danych nie powiodła się. Szczegóły zapisano w logu.",
        IOException => "Operacja plikowa nie powiodła się. Sprawdź miejsce na dysku, uprawnienia i blokady plików.",
        ArgumentException or FormatException or InvalidDataException => "Dane wejściowe są nieprawidłowe lub mają nieobsługiwany format.",
        _ => "Wystąpił nieoczekiwany błąd. Szczegóły zapisano w logu."
    };
}
