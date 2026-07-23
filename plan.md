# Plan prac projektu CSVForge

Status: MVP ukończone, zweryfikowane i gotowe do publikacji.

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

- [x] Uzupełnić testy blokady SQLite, pustych kluczy i kodów wyjścia CLI.
- [x] Wykonać pełny build i testy w konfiguracjach Debug oraz Release.
- [x] Zweryfikować oba profile publikacji, ZIP-y i MSIX.
- [x] Wykonać test dymny CLI na świeżym workspace.
- [x] Sprawdzić czystość repozytorium i wypchnąć wszystkie commity do GitHub.

## Etap 2 — inteligentny edytor SQL

Cel: zastąpić podstawowe pole tekstowe SQL wygodnym edytorem z kolorowaniem składni i podpowiedziami dopasowanymi do aktywnego workspace.

Status etapu: ukończony.

### 2.1. Fundament edytora

- [x] Dodać AvalonEdit do projektu WPF i zastąpić nim obecne pole zapytania SQL.
- [x] Przygotować jasny motyw zgodny z wyglądem CSVForge.
- [x] Włączyć numerację wierszy, wyróżnianie bieżącego wiersza i przewijanie w obu kierunkach.
- [x] Dodać kolorowanie składni SQLite: słowa kluczowe, funkcje, ciągi znaków, liczby, komentarze i identyfikatory.
- [x] Zachować wykonanie zapytania przez przycisk oraz `Ctrl+Enter`.

### 2.2. Podstawowe podpowiedzi

- [x] Przygotować katalog słów kluczowych, funkcji i operatorów SQLite.
- [x] Otwierać listę podpowiedzi podczas pisania oraz skrótem `Ctrl+Space`.
- [x] Filtrować propozycje bez rozróżniania wielkości liter i zatwierdzać je przez `Enter`, `Tab` lub kliknięcie.
- [x] Dodać ikony lub kategorie rozróżniające słowa SQL, funkcje, tabele i kolumny.
- [x] Automatycznie domykać nawiasy, apostrofy i cudzysłowy bez dublowania znaków zamykających.

### 2.3. Schemat aktywnego workspace

- [x] Dodać port i use case odczytujący tabele, widoki oraz ich kolumny z metadanych SQLite.
- [x] Odświeżać katalog podpowiedzi po zmianie workspace, imporcie danych i wykonaniu SQL zmieniającego schemat.
- [x] Podpowiadać nazwy tabel i widoków po `FROM`, `JOIN`, `UPDATE`, `INSERT INTO` oraz `DELETE FROM`.
- [x] Podpowiadać kolumny w sekcjach `SELECT`, `WHERE`, `ON`, `GROUP BY`, `HAVING` i `ORDER BY`.
- [x] Poprawnie cytować nazwy zawierające spacje, znaki specjalne lub słowa zastrzeżone.

### 2.4. Podpowiedzi kontekstowe

- [x] Rozpoznawać aliasy tabel z klauzul `FROM` i `JOIN`.
- [x] Po wpisaniu `alias.` pokazywać wyłącznie kolumny przypisanej tabeli.
- [x] Ograniczać propozycje kolumn do tabel użytych w bieżącym zapytaniu.
- [x] Obsłużyć wiele instrukcji SQL i ustalać kontekst względem pozycji kursora.
- [x] Zapewnić sensowne podpowiedzi awaryjne dla niepełnego lub chwilowo niepoprawnego zapytania.

### 2.5. Jakość i testy

- [x] Wydzielić analizę kontekstu i budowanie propozycji poza warstwę widoku WPF.
- [x] Dodać testy jednostkowe filtrowania, rankingu, aliasów, cytowania identyfikatorów i kontekstu kursora.
- [x] Dodać testy integracyjne odświeżania schematu po imporcie oraz poleceniach `CREATE`, `ALTER` i `DROP`.
- [x] Sprawdzić płynność edytora dla dużych zapytań i workspace z setkami tabel oraz tysiącami kolumn.
- [x] Zweryfikować obsługę klawiatury, focus, skalowanie DPI i czytelność listy podpowiedzi.
- [x] Uzupełnić README o skróty klawiszowe i przykłady korzystania z podpowiedzi SQL.

### Kryteria ukończenia etapu 2

- Edytor koloruje poprawnie składnię SQLite i nie blokuje interfejsu podczas pisania.
- `Ctrl+Space` zawsze otwiera podpowiedzi, a automatyczna lista pojawia się w odpowiednim kontekście.
- Po `FROM` i `JOIN` dostępne są tabele, a po `alias.` wyłącznie kolumny właściwej tabeli.
- Zmiany schematu i zakończone importy są widoczne w podpowiedziach bez restartowania aplikacji.
- Wszystkie nowe testy oraz pełna regresja Debug i Release przechodzą bez ostrzeżeń.

## Roadmapa po MVP

Poniższe funkcje nie blokują wydania MVP:

- Eksport i import plików Excel `.xlsx`.
- Zapisywane scenariusze importu, porównania i łączenia.
- Automatyczne typowanie, transformacje i czyszczenie kolumn.
- Deduplikacja z wyborem rekordu głównego i fuzzy matching.
- Silnik DuckDB dla jeszcze większych zbiorów.
- Ciemny motyw GUI.
