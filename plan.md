# Plan prac projektu CSVForge

1. **Przygotowanie solution**
   1. Utworzyć `CSVForge.sln`
   2. Utworzyć projekt `CSVForge.App.Wpf`
   3. Utworzyć projekt `CSVForge.Application`
   4. Utworzyć projekt `CSVForge.Domain`
   5. Utworzyć projekt `CSVForge.Infrastructure`
   6. Utworzyć projekt `CSVForge.Cli`
   7. Utworzyć projekt `CSVForge.Tests`
   8. Ustawić referencje między projektami
   9. Dodać podstawowy Dependency Injection
   10. Dodać podstawowy logging przez Serilog

2. **Zaprojektowanie warstwy Domain**
   1. Dodać model `Workspace`
   2. Dodać model `CsvImport`
   3. Dodać model `CsvColumn`
   4. Dodać model `ImportRequest`
   5. Dodać model `ImportResult`
   6. Dodać model `ImportError`
   7. Dodać model `ImportProgress`
   8. Dodać model `OperationResult`
   9. Dodać model `DuplicateSearchRequest`
   10. Dodać model `DatasetCompareRequest`
   11. Dodać model `DatasetJoinRequest`
   12. Dodać reguL�y walidacji nazw tabel i kolumn

3. **Zaprojektowanie warstwy Application**
   1. Dodać use case `CreateWorkspaceUseCase`
   2. Dodać use case `OpenWorkspaceUseCase`
   3. Dodać use case `ImportCsvUseCase`
   4. Dodać use case `PreviewCsvUseCase`
   5. Dodać use case `ListImportedTablesUseCase`
   6. Dodać use case `BrowseTableUseCase`
   7. Dodać use case `FindDuplicatesUseCase`
   8. Dodać use case `CompareDatasetsUseCase`
   9. Dodać use case `JoinDatasetsUseCase`
   10. Dodać use case `ExportTableUseCase`
   11. Zdefiniować interfejs `IWorkspaceService`
   12. Zdefiniować interfejs `ICsvReader`
   13. Zdefiniować interfejs `ICsvImporter`
   14. Zdefiniować interfejs `ITableBrowser`
   15. Zdefiniować interfejs `IDuplicateFinder`
   16. Zdefiniować interfejs `IDatasetComparer`
   17. Zdefiniować interfejs `IDatasetJoiner`
   18. Zdefiniować interfejs `ITableExporter`

4. **Implementacja SQLite workspace**
   1. Dodać konfigurację poL�ączenia do SQLite
   2. Dodać tworzenie pliku workspace `.db`
   3. Dodać otwieranie istniejącego workspace
   4. Dodać migracje schematu
   5. Utworzyć tabelę `_workspace_imports`
   6. Utworzyć tabelę `_workspace_columns`
   7. Utworzyć tabelę `_workspace_errors`
   8. Utworzyć tabelę `_workspace_operations`
   9. Dodać repozytorium metadanych workspace
   10. Dodać bezpieczne generowanie nazw tabel roboczych
   11. Dodać bezpieczne escapowanie nazw tabel i kolumn
   12. Dodać usuwanie importu z workspace

5. **Implementacja odczytu CSV**
   1. Dodać `CsvHelper`
   2. Dodać wykrywanie separatora
   3. Dodać obsL�ugę separatora `;`
   4. Dodać obsL�ugę separatora `,`
   5. Dodać obsL�ugę separatora tab
   6. Dodać obsL�ugę UTF-8
   7. Dodać obsL�ugę UTF-8 BOM
   8. Dodać obsL�ugę Windows-1250
   9. Dodać wykrywanie nagL�AlwkAlw
   10. Dodać obsL�ugę plikAlw bez nagL�Alwka
   11. Dodać normalizację nazw kolumn
   12. Dodać obsL�ugę zduplikowanych nazw kolumn
   13. Dodać podgląd pierwszych 100 wierszy
   14. Dodać raport bL�ędAlw parsowania

6. **Implementacja importu CSV do SQLite**
   1. Utworzyć tabelę roboczą dla importowanego pliku
   2. Importować wszystkie kolumny jako `TEXT`
   3. Czytać CSV strumieniowo
   4. Wstawiać dane partiami
   5. ULLyć transakcji dla batchy
   6. Dodać konfigurację rozmiaru batcha
   7. Zapisywać metadane importu
   8. Zapisywać metadane kolumn
   9. Zapisywać bL�ędne wiersze do `_workspace_errors`
   10. Raportować postęp importu
   11. Dodać anulowanie importu przez `CancellationToken`
   12. Dodać podsumowanie importu

7. **Implementacja przeglądania danych**
   1. Dodać pobieranie listy importAlw
   2. Dodać pobieranie listy kolumn tabeli
   3. Dodać licznik wierszy tabeli
   4. Dodać pobieranie danych z `LIMIT/OFFSET`
   5. Dodać paginację
   6. Dodać sortowanie po kolumnie
   7. Dodać proste filtrowanie tekstowe
   8. Dodać zabezpieczenie przed L�adowaniem caL�ej tabeli do pamięci
   9. Dodać obsL�ugę pustych tabel
   10. Dodać obsL�ugę bardzo szerokich tabel

8. **Implementacja wyszukiwania duplikatAlw**
   1. Dodać wybAlr tabeli
   2. Dodać wybAlr jednej kolumny
   3. Dodać wybAlr wielu kolumn jako klucza duplikatu
   4. Dodać tworzenie indeksu na kolumnach-kluczach
   5. Dodać tryb "podsumowanie duplikatAlw"
   6. Dodać tryb "wszystkie wiersze naleLLące do duplikatAlw"
   7. Zapisywać wynik jako nową tabelę roboczą
   8. Zapisywać metadane operacji
   9. Dodać podgląd wyniku
   10. Dodać obsL�ugę pustych wartoL�ci
   11. Dodać opcję ignorowania pustych wartoL�ci
   12. Dodać raport liczby znalezionych duplikatAlw

9. **Implementacja porAlwnywania dwAlch tabel**
   1. Dodać wybAlr tabeli A
   2. Dodać wybAlr tabeli B
   3. Dodać wybAlr kolumny-klucza z tabeli A
   4. Dodać wybAlr kolumny-klucza z tabeli B
   5. Dodać obsL�ugę klucza zL�oLLonego z wielu kolumn
   6. Dodać indeksowanie kolumn-kluczy
   7. Dodać tryb "rekordy wspAllne"
   8. Dodać tryb "tylko w tabeli A"
   9. Dodać tryb "tylko w tabeli B"
   10. Dodać tryb "wszystko ze statusem"
   11. Zapisywać wynik jako nową tabelę roboczą
   12. Zapisywać metadane operacji
   13. Dodać podgląd wyniku
   14. Dodać raport liczby rekordAlw w kaLLdym statusie

10. **Implementacja L�ączenia danych**
    1. Dodać wybAlr tabeli A
    2. Dodać wybAlr tabeli B
    3. Dodać wybAlr kolumn L�ączenia
    4. Dodać obsL�ugę wielu kolumn L�ączenia
    5. Dodać wybAlr kolumn wynikowych z tabeli A
    6. Dodać wybAlr kolumn wynikowych z tabeli B
    7. Dodać obsL�ugę konfliktAlw nazw kolumn
    8. Dodać tryb `INNER JOIN`
    9. Dodać tryb `LEFT JOIN`
    10. Dodać tryb `RIGHT JOIN` przez odwrAlcenie tabel
    11. Zapisywać wynik jako nową tabelę roboczą
    12. Zapisywać metadane operacji
    13. Dodać podgląd wyniku
    14. Dodać raport liczby poL�ączonych rekordAlw

11. **Implementacja eksportu danych**
    1. Dodać wybAlr tabeli do eksportu
    2. Dodać eksport do CSV
    3. Dodać wybAlr L�cieLLki pliku
    4. Dodać wybAlr separatora
    5. Dodać domyL�lne kodowanie UTF-8 BOM
    6. Dodać eksport danych strumieniowo
    7. Dodać eksport przefiltrowanych danych
    8. Dodać eksport wynikAlw operacji
    9. Dodać raport zakoL�czenia eksportu
    10. Opcjonalnie dodać eksport do `.xlsx`

12. **Implementacja GUI WPF**
    1. Dodać gL�Alwne okno aplikacji
    2. Dodać nawigację między ekranami
    3. Dodać ekran startowy workspace
    4. Dodać ekran importu CSV
    5. Dodać ekran podglądu CSV przed importem
    6. Dodać ekran listy zaimportowanych tabel
    7. Dodać ekran przeglądania danych
    8. Dodać ekran wyszukiwania duplikatAlw
    9. Dodać ekran porAlwnywania tabel
    10. Dodać ekran L�ączenia danych
    11. Dodać ekran eksportu
    12. Dodać panel historii operacji
    13. Dodać dialog wyboru pliku
    14. Dodać progress bar dla dL�ugich operacji
    15. Dodać przycisk anulowania operacji
    16. Dodać komunikaty bL�ędAlw po polsku
    17. Dodać walidację formularzy
    18. Dodać DataGrid z paginacją

13. **Implementacja CLI**
    1. Dodać komendę `workspace create`
    2. Dodać komendę `workspace open`
    3. Dodać komendę `import`
    4. Dodać komendę `list-tables`
    5. Dodać komendę `duplicates`
    6. Dodać komendę `compare`
    7. Dodać komendę `join`
    8. Dodać komendę `export`
    9. Dodać obsL�ugę parametrAlw wejL�ciowych
    10. Dodać logowanie do konsoli
    11. Dodać kody wyjL�cia
    12. Dodać przykL�ady uLLycia

14. **ObsL�uga bL�ędAlw i przypadkAlw brzegowych**
    1. ObsL�uLLyć brak pliku CSV
    2. ObsL�uLLyć pusty CSV
    3. ObsL�uLLyć CSV bez nagL�AlwkAlw
    4. ObsL�uLLyć zduplikowane nagL�Alwki
    5. ObsL�uLLyć bL�ędną liczbę kolumn w wierszu
    6. ObsL�uLLyć bardzo dL�ugie pola tekstowe
    7. ObsL�uLLyć polskie znaki
    8. ObsL�uLLyć nieprawidL�ową L�cieLLkę workspace
    9. ObsL�uLLyć zablokowany plik SQLite
    10. ObsL�uLLyć przerwanie importu
    11. ObsL�uLLyć przerwanie eksportu
    12. ObsL�uLLyć brak miejsca na dysku
    13. ObsL�uLLyć konflikty nazw tabel
    14. ObsL�uLLyć konflikty nazw kolumn
    15. ObsL�uLLyć puste wartoL�ci w kolumnach-kluczach

15. **Testy jednostkowe**
    1. Przetestować walidację nazw tabel
    2. Przetestować walidację nazw kolumn
    3. Przetestować normalizację nagL�AlwkAlw CSV
    4. Przetestować wykrywanie separatora
    5. Przetestować budowanie LLądaL� importu
    6. Przetestować konfigurację duplikatAlw
    7. Przetestować konfigurację porAlwnania
    8. Przetestować konfigurację joinAlw
    9. Przetestować obsL�ugę bL�ędAlw walidacji
    10. Przetestować generowanie bezpiecznych nazw tabel wynikowych

16. **Testy integracyjne**
    1. Przetestować utworzenie workspace SQLite
    2. Przetestować import CSV do SQLite
    3. Przetestować import CSV z separatorem `;`
    4. Przetestować import CSV z separatorem `,`
    5. Przetestować import CSV z polskimi znakami
    6. Przetestować import CSV bez nagL�Alwka
    7. Przetestować wyszukiwanie duplikatAlw
    8. Przetestować porAlwnanie dwAlch tabel
    9. Przetestować `INNER JOIN`
    10. Przetestować `LEFT JOIN`
    11. Przetestować eksport wyniku do CSV
    12. Przetestować anulowanie importu

17. **Testy wydajnoL�ciowe**
    1. Przygotować plik testowy 10 tys. wierszy
    2. Przygotować plik testowy 50 tys. wierszy
    3. Przygotować plik testowy 100 tys. wierszy
    4. Zmierzyć czas importu
    5. Zmierzyć czas wyszukiwania duplikatAlw
    6. Zmierzyć czas porAlwnania dwAlch tabel
    7. Zmierzyć czas joinowania dwAlch tabel
    8. Zmierzyć zuLLycie pamięci podczas importu
    9. Zmierzyć zuLLycie pamięci podczas podglądu danych
    10. Dobrać optymalny rozmiar batcha
    11. Sprawdzić skutecznoL�ć indeksAlw

18. **Dopracowanie UX**
    1. Dodać historię ostatnich workspace'Alw
    2. Dodać historię ostatnich importAlw
    3. Dodać historię operacji
    4. Dodać czytelne podsumowania operacji
    5. Dodać moLLliwoL�ć zmiany nazw importAlw
    6. Dodać moLLliwoL�ć usunięcia wynikAlw operacji
    7. Dodać potwierdzenia dla operacji destrukcyjnych
    8. Dodać polskie komunikaty bL�ędAlw
    9. Dodać ekran logAlw aplikacji
    10. Dodać prostą dokumentację w aplikacji

19. **Pakowanie i dystrybucja**
    1. Przygotować konfigurację publikacji WPF
    2. Przygotować build `self-contained`
    3. Przygotować build `framework-dependent`
    4. Dodać ikonę aplikacji
    5. Dodać wersjonowanie aplikacji
    6. Przygotować installer MSIX albo MSI
    7. Przygotować katalog przykL�adowych plikAlw CSV
    8. Przygotować instrukcję instalacji
    9. Przygotować instrukcję uLLycia CLI
    10. Przygotować changelog

20. **Roadmapa po MVP**
    1. Dodać eksport do Excela `.xlsx`
    2. Dodać zapisywanie scenariuszy importu
    3. Dodać zapisywanie scenariuszy porAlwnania
    4. Dodać zapisywanie scenariuszy joinAlw
    5. Dodać automatyczne typowanie kolumn
    6. Dodać transformacje kolumn
    7. Dodać czyszczenie danych
    8. Dodać deduplikację z wyborem rekordu gL�Alwnego
    9. Dodać fuzzy matching
    10. Dodać obsL�ugę większych plikAlw przez DuckDB
    11. Dodać import z Excela
    12. Dodać ciemny motyw GUI
