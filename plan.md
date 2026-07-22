# Plan prac projektu CSVForge

Status: implementacja MVP zakończona funkcjonalnie; trwa końcowy audyt i publikacja.

## Zakres MVP

- [x] Solution .NET 10 z projektami WPF, CLI, Application, Domain, Infrastructure i Tests.
- [x] Dependency Injection oraz logowanie Serilog do pliku i konsoli CLI.
- [x] Modele domenowe, use case'y i porty dla workspace, importu, tabel i operacji.
- [x] Workspace SQLite z migracjami, metadanymi, historią, błędami i bezpiecznymi identyfikatorami.
- [x] Import CSV strumieniowy, transakcyjny i partiami z postępem oraz anulowaniem.
- [x] Separatory przecinek, średnik i tabulator oraz kodowania UTF-8, UTF-8 BOM i Windows-1250.
- [x] Automatyczne wykrywanie separatora, kodowania i obecności nagłówka.
- [x] Pliki bez nagłówka, normalizacja i deduplikacja nazw kolumn oraz raport błędnych wierszy.
- [x] Podgląd CSV oraz stronicowane przeglądanie tabel z filtrowaniem i sortowaniem po stronie SQLite.
- [x] Wyszukiwanie duplikatów po jednym lub wielu kluczach, z opcją pomijania pustych wartości.
- [x] Porównywanie tabel po kluczach prostych i złożonych we wszystkich zaplanowanych trybach.
- [x] Łączenie tabel INNER, LEFT i RIGHT z wyborem kluczy oraz kolumn wynikowych.
- [x] Indeksy robocze, tabele wynikowe, historia operacji i podsumowania liczby rekordów.
- [x] Strumieniowy eksport CSV z filtrem, wyborem separatora, UTF-8 BOM i anulowaniem.
- [x] GUI WPF dla pełnego przepływu: workspace, import, tabele, operacje, eksport, logi i pomoc.
- [x] Responsywny pasek operacji, walidacja formularzy, polskie błędy, postęp i potwierdzenia.
- [x] CLI dla tworzenia i otwierania workspace, importu, listowania, operacji oraz eksportu.
- [x] Obsługa kodów wyjścia i przykłady użycia CLI.
- [x] Testy jednostkowe i integracyjne kluczowych przepływów oraz przypadków brzegowych CSV.
- [x] Generatory danych i pomiary wydajności dla 10 tys., 50 tys. i 100 tys. wierszy.
- [x] Dokumentacja budowania, użycia, wydajności i dystrybucji.
- [x] Publikacja framework-dependent i self-contained, paczki ZIP oraz instalator MSIX.
- [x] Ikona, wersjonowanie, przykładowy CSV i changelog.

## Końcowy audyt

- [ ] Uzupełnić testy blokady SQLite, pustych kluczy i kodów wyjścia CLI.
- [ ] Wykonać pełny build i testy w konfiguracjach Debug oraz Release.
- [ ] Zweryfikować oba profile publikacji, ZIP-y i MSIX.
- [ ] Wykonać test dymny CLI na świeżym workspace.
- [ ] Sprawdzić czystość repozytorium i wypchnąć wszystkie commity do GitHub.

## Roadmapa po MVP

Poniższe funkcje nie blokują wydania MVP:

- Eksport i import plików Excel `.xlsx`.
- Zapisywane scenariusze importu, porównania i łączenia.
- Automatyczne typowanie, transformacje i czyszczenie kolumn.
- Deduplikacja z wyborem rekordu głównego i fuzzy matching.
- Silnik DuckDB dla jeszcze większych zbiorów.
- Ciemny motyw GUI.
