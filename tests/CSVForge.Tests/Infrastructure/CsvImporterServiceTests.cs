using System.Text;
using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Ports;
using CSVForge.Application.Tables;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class CsvImporterServiceTests
{
    [Fact]
    public async Task ImportCsvUseCase_ImportsRowsAsTextAndStoresMetadata()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "people.csv");
        await File.WriteAllTextAsync(csvPath, "Name;Age\r\nAda;42\r\nOla;7\r\n");

        ServiceProvider provider = new ServiceCollection()
            .AddApplication()
            .AddInfrastructure()
            .BuildServiceProvider();

        ICreateWorkspaceUseCase createWorkspace = provider.GetRequiredService<ICreateWorkspaceUseCase>();
        IImportCsvUseCase importCsv = provider.GetRequiredService<IImportCsvUseCase>();

        await createWorkspace.ExecuteAsync(workspacePath);
        ImportResult result = await importCsv.ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null));

        Assert.Equal(2, result.Import.RowCount);
        Assert.Equal(["Name", "Age"], result.Import.Columns.Select(column => column.Name));

        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();

        await using SqliteCommand rowCountCommand = connection.CreateCommand();
        rowCountCommand.CommandText = $"SELECT COUNT(*) FROM \"{result.Import.TableName}\";";
        Assert.Equal(2L, (long)(await rowCountCommand.ExecuteScalarAsync() ?? 0L));

        await using SqliteCommand metadataCommand = connection.CreateCommand();
        metadataCommand.CommandText = "SELECT COUNT(*) FROM _workspace_imports WHERE id = $id;";
        metadataCommand.Parameters.AddWithValue("$id", result.Import.Id.ToString());
        Assert.Equal(1L, (long)(await metadataCommand.ExecuteScalarAsync() ?? 0L));
    }

    [Fact]
    public async Task ImportCsvUseCase_SkipsAndPersistsRowsWithWrongFieldCount()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "broken.csv");
        await File.WriteAllTextAsync(csvPath, "Name;Age\r\nAda;42\r\nOla\r\nZen;7;extra\r\n");

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Broken", true, null, null));

        Assert.Equal(1, result.Import.RowCount);
        Assert.Equal(2, result.Errors.Count);

        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM _workspace_errors WHERE import_id = $importId;";
        command.Parameters.AddWithValue("$importId", result.Import.Id.ToString());
        Assert.Equal(2L, (long)(await command.ExecuteScalarAsync() ?? 0L));
    }

    [Fact]
    public async Task ImportCsvUseCase_ThrowsForMissingFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(Path.Combine(directory, "workspace.db"));

        await Assert.ThrowsAsync<FileNotFoundException>(() => provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(Path.Combine(directory, "missing.csv"), "Missing", true, null, null)));
    }

    [Fact]
    public async Task ImportCsvUseCase_CancellationRemovesPartiallyImportedTable()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "large.csv");
        await File.WriteAllLinesAsync(csvPath, ["Id", .. Enumerable.Range(1, 1200).Select(value => value.ToString())]);

        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        using CancellationTokenSource cancellation = new();
        CancelOnProgress progress = new(cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Large", true, null, null, 500), progress, cancellation.Token));

        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE '\_%' ESCAPE '\'
              AND name NOT LIKE 'sqlite_%';
            """;
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync() ?? 0L));
        Assert.Empty(await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync());
    }

    [Fact]
    public async Task ImportCsvUseCase_AutomaticallyImportsWindows1250PolishText()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "polish.csv");
        await File.WriteAllTextAsync(csvPath, "Nazwa;Miasto\r\nŻółw;Łódź\r\n", Encoding.GetEncoding("windows-1250"));
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);

        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Polish", true, null, null));

        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT Miasto FROM \"{result.Import.TableName}\";";
        Assert.Equal("Łódź", (string?)await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ImportCsvUseCase_UsesConfiguredBatchSizeAndReportsProgress()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string csvPath = Path.Combine(directory, "data.csv");
        await File.WriteAllTextAsync(csvPath, "Id\r\n1\r\n2\r\n3\r\n4\r\n5\r\n");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(Path.Combine(directory, "workspace.db"));
        List<ImportProgress> reports = [];
        InlineProgress progress = new(reports.Add);

        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Data", true, null, null, 2), progress);

        Assert.Equal(5, result.Import.RowCount);
        Assert.Contains(reports, item => item.ProcessedRows == 2);
        Assert.Contains(reports, item => item.ProcessedRows == 4);
    }

    [Fact]
    public async Task ImportCsvUseCase_FirstCommittedBatchIsVisibleBeforeImportCompletes()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "data.csv");
        await File.WriteAllTextAsync(csvPath, "Id\r\n1\r\n2\r\n3\r\n4\r\n5\r\n");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        bool firstBatchWasVisible = false;
        InlineProgress progress = new(value =>
        {
            if (value.CurrentStep != "Batch committed" || firstBatchWasVisible)
            {
                return;
            }

            using SqliteConnection connection = new($"Data Source={workspacePath}");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM _workspace_imports AS imports
                JOIN sqlite_master AS tables ON tables.name = imports.table_name
                WHERE imports.row_count = 2;
                """;
            firstBatchWasVisible = (long)(command.ExecuteScalar() ?? 0L) == 1;
        });

        await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Data", true, null, null, 2), progress);

        Assert.True(firstBatchWasVisible);
    }

    [Fact]
    public async Task ImportCsvUseCase_ImportsHeaderOnlyAsEmptyTable()
    {
        (ServiceProvider provider, string csvPath) = await CreateWorkspaceAndCsvAsync("Name;City\r\n");

        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Empty", true, null, null));

        Assert.Equal(0, result.Import.RowCount);
        Assert.Equal(["Name", "City"], result.Import.Columns.Select(column => column.Name));
    }

    [Fact]
    public async Task ImportCsvUseCase_PreservesVeryLongTextField()
    {
        string value = new('x', 100_000);
        (ServiceProvider provider, string csvPath) = await CreateWorkspaceAndCsvAsync($"Value\r\n{value}\r\n");

        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Long", true, null, null));

        Assert.Equal(1, result.Import.RowCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportCsvUseCase_TruncatesLongTableAndColumnIdentifiers()
    {
        string longName = new('a', 120);
        (ServiceProvider provider, string csvPath) = await CreateWorkspaceAndCsvAsync($"{longName};{longName}\r\n1;2\r\n");

        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, longName, true, null, null));

        Assert.True(result.Import.TableName.Length <= 64);
        Assert.All(result.Import.Columns, column => Assert.True(column.Name.Length <= 64));
        Assert.Equal(2, result.Import.Columns.Select(column => column.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task ImportCsvUseCase_ImportsReportWithPreambleAndTrailingDelimiter()
    {
        (ServiceProvider provider, string csvPath) = await CreateWorkspaceAndCsvAsync(
            "Obiekty: od 1 do 3 ze wszystkich 3\r\nPPE;\r\n590380100003453588;\r\n590380100012575219;\r\n");

        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Energy", true, null, null, 500, true));

        Assert.Equal(2, result.Import.RowCount);
        Assert.Equal(["PPE"], result.Import.Columns.Select(column => column.Name));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportCsvUseCase_AppliesColumnNamesTypesAndSelection()
    {
        (ServiceProvider provider, string csvPath) = await CreateWorkspaceAndCsvAsync(
            "Name;Age;Price;Active;Ignored\r\nAda;42;12,50;tak;x\r\nOla;7;3,25;nie;y\r\n");
        CsvColumnMapping[] mappings =
        [
            new(0, "Customer", CsvColumnDataType.Text),
            new(1, "Years", CsvColumnDataType.Integer),
            new(2, "Amount", CsvColumnDataType.Decimal),
            new(3, "Enabled", CsvColumnDataType.Boolean),
            new(4, "Ignored", CsvColumnDataType.Text, Include: false)
        ];

        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "Typed", true, null, null, 500, false, mappings));

        Assert.Equal(2, result.Import.RowCount);
        Assert.Equal(["Customer", "Years", "Amount", "Enabled"], result.Import.Columns.Select(column => column.Name));
        Assert.Empty(result.Errors);

        string workspacePath = provider.GetRequiredService<CSVForge.Application.Ports.IWorkspaceContext>().CurrentWorkspacePath!;
        await using SqliteConnection connection = new($"Data Source={workspacePath}");
        await connection.OpenAsync();
        await using SqliteCommand schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = $"PRAGMA table_info(\"{result.Import.TableName}\");";
        List<(string Name, string Type)> schema = [];
        await using (SqliteDataReader reader = await schemaCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                schema.Add((reader.GetString(1), reader.GetString(2)));
            }
        }

        Assert.Equal([("Customer", "TEXT"), ("Years", "INTEGER"), ("Amount", "REAL"), ("Enabled", "INTEGER")], schema);
        await using SqliteCommand valueCommand = connection.CreateCommand();
        valueCommand.CommandText = $"SELECT Years, Amount, Enabled FROM \"{result.Import.TableName}\" WHERE Customer = 'Ada';";
        await using SqliteDataReader valueReader = await valueCommand.ExecuteReaderAsync();
        Assert.True(await valueReader.ReadAsync());
        Assert.Equal(42L, valueReader.GetInt64(0));
        Assert.Equal(12.5, valueReader.GetDouble(1));
        Assert.Equal(1L, valueReader.GetInt64(2));
    }

    [Fact]
    public async Task ImportCsvUseCase_UsesOriginalSourcePathForStagedFile()
    {
        (ServiceProvider provider, string stagedPath) = await CreateWorkspaceAndCsvAsync("Id;Name\r\n1;Ada\r\n");
        string originalPath = Path.Combine(Path.GetDirectoryName(stagedPath)!, "incoming", "report.csv");

        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>().ExecuteAsync(
            new ImportRequest(stagedPath, "Report", true, null, null, SourcePath: originalPath));

        Assert.Equal(originalPath, result.Import.SourcePath);
        IReadOnlyList<CsvImport> imports = await provider.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync();
        Assert.Equal(originalPath, Assert.Single(imports).SourcePath);
    }

    [Fact]
    public async Task ImportCsvUseCase_PromotesPreparedSqliteTableWithoutReparsingChangedCsv()
    {
        (ServiceProvider provider, string csvPath) = await CreateWorkspaceAndCsvAsync("Id;Name\r\n1;Ada\r\n2;Ola\r\n");
        ICsvStagingService stagingService = provider.GetRequiredService<ICsvStagingService>();
        CsvStagingResult staging = await stagingService.StageAsync(
            new ImportRequest(csvPath, "Prepared", true, null, null, AutoDetectHeader: true), CancellationToken.None);
        await File.WriteAllTextAsync(csvPath, "Id;Name\r\n9;Changed\r\n");

        ImportResult result = await provider.GetRequiredService<IImportCsvUseCase>().ExecuteAsync(new ImportRequest(
            csvPath,
            "Promoted",
            true,
            null,
            null,
            AutoDetectHeader: true,
            ColumnMappings: staging.Preview.Columns.Select(column => new CsvColumnMapping(column.Index, column.Name, CsvColumnDataType.Text)).ToArray(),
            StagingDatabasePath: staging.DatabasePath,
            StagingTableName: staging.TableName));

        Assert.Equal(2, result.Import.RowCount);
        TablePage page = await provider.GetRequiredService<IBrowseTableUseCase>().ExecuteAsync(
            new BrowseTableRequest(result.Import.TableName, 10, 0, null, false));
        Assert.Equal("Ada", page.Rows[0]["Name"]);
        Assert.Equal("Ola", page.Rows[1]["Name"]);
        SqliteConnection.ClearAllPools();
        File.Delete(staging.DatabasePath);
    }

    private static async Task<(ServiceProvider Provider, string CsvPath)> CreateWorkspaceAndCsvAsync(string content)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string csvPath = Path.Combine(directory, "data.csv");
        await File.WriteAllTextAsync(csvPath, content);
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(Path.Combine(directory, "workspace.db"));
        return (provider, csvPath);
    }

    private sealed class CancelOnProgress(CancellationTokenSource cancellation) : IProgress<ImportProgress>
    {
        public void Report(ImportProgress value)
        {
            if (value.ProcessedRows >= 500)
            {
                cancellation.Cancel();
            }
        }
    }

    private sealed class InlineProgress(Action<ImportProgress> report) : IProgress<ImportProgress>
    {
        public void Report(ImportProgress value) => report(value);
    }
}
