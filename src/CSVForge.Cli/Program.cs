using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Export;
using CSVForge.Domain.Imports;
using CSVForge.Domain.Operations;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

return await CliApplication.RunAsync(args);

public static class CliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File("logs/csvforge-cli-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            using IHost host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices(services => services.AddApplication().AddInfrastructure())
                .Build();

            return await ExecuteAsync(host.Services, args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Błąd parametrów: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CSVForge CLI failed.");
            Console.Error.WriteLine($"Błąd: {ex.Message}");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        Dictionary<string, string> options = ParseOptions(args.Skip(1).ToArray());
        string command = args[0].ToLowerInvariant();

        if (command == "workspace")
        {
            return await RunWorkspaceAsync(services, options);
        }

        string workspacePath = Required(options, "workspace");
        await services.GetRequiredService<IOpenWorkspaceUseCase>().ExecuteAsync(workspacePath);

        switch (command)
        {
            case "import":
                {
                    string file = Required(options, "file");
                    string name = options.GetValueOrDefault("name") ?? Path.GetFileNameWithoutExtension(file);
                    ImportResult result = await services.GetRequiredService<IImportCsvUseCase>()
                        .ExecuteAsync(new ImportRequest(file, name, !options.ContainsKey("no-header"), Delimiter(options), options.GetValueOrDefault("encoding"), IntOption(options, "batch-size", 5000), !options.ContainsKey("no-header")));
                    Console.WriteLine($"Zaimportowano {result.Import.RowCount} wierszy do {result.Import.TableName}.");
                    return 0;
                }
            case "list-tables":
                {
                    IReadOnlyList<CsvImport> imports = await services.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync();
                    foreach (CsvImport import in imports)
                    {
                        Console.WriteLine($"{import.TableName}\t{import.RowCount}\t{import.DisplayName}");
                    }
                    return 0;
                }
            case "duplicates":
                {
                    OperationResult result = await services.GetRequiredService<IFindDuplicatesUseCase>().ExecuteAsync(new DuplicateSearchRequest(
                        Required(options, "table"), Columns(options, "columns"),
                        EnumOption(options, "mode", DuplicateSearchMode.AllDuplicateRows), !options.ContainsKey("include-empty")));
                    PrintOperation(result);
                    return 0;
                }
            case "compare":
                {
                    OperationResult result = await services.GetRequiredService<ICompareDatasetsUseCase>().ExecuteAsync(new DatasetCompareRequest(
                        Required(options, "left"), Required(options, "right"), Columns(options, "left-keys"), Columns(options, "right-keys"),
                        EnumOption(options, "mode", DatasetCompareMode.AllWithStatus)));
                    PrintOperation(result);
                    return 0;
                }
            case "join":
                {
                    IReadOnlyList<CsvImport> imports = await services.GetRequiredService<IListImportedTablesUseCase>().ExecuteAsync();
                    string left = Required(options, "left");
                    string right = Required(options, "right");
                    IReadOnlyList<string> leftOutput = OptionalColumns(options, "left-output") ?? FindColumns(imports, left);
                    IReadOnlyList<string> rightOutput = OptionalColumns(options, "right-output") ?? FindColumns(imports, right);
                    OperationResult result = await services.GetRequiredService<IJoinDatasetsUseCase>().ExecuteAsync(new DatasetJoinRequest(
                        left, right, Columns(options, "left-keys"), Columns(options, "right-keys"), leftOutput, rightOutput,
                        EnumOption(options, "type", DatasetJoinType.Left)));
                    PrintOperation(result);
                    return 0;
                }
            case "export":
                {
                    ExportResult result = await services.GetRequiredService<IExportTableUseCase>().ExecuteAsync(new ExportTableRequest(
                        Required(options, "table"), Required(options, "output"), Delimiter(options) ?? ';', !options.ContainsKey("no-header")));
                    Console.WriteLine($"Wyeksportowano {result.ExportedRows} wierszy do {result.FilePath}.");
                    return 0;
                }
            default:
                throw new ArgumentException($"Nieznana komenda '{command}'. Użyj --help.");
        }
    }

    static async Task<int> RunWorkspaceAsync(IServiceProvider services, IReadOnlyDictionary<string, string> options)
    {
        string action = Required(options, "action").ToLowerInvariant();
        string path = Required(options, "path");
        if (action == "create")
        {
            await services.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(path);
            Console.WriteLine($"Utworzono workspace: {path}");
            return 0;
        }
        if (action == "open")
        {
            await services.GetRequiredService<IOpenWorkspaceUseCase>().ExecuteAsync(path);
            Console.WriteLine($"Workspace jest poprawny: {path}");
            return 0;
        }
        throw new ArgumentException("Opcja --action musi mieć wartość create albo open.");
    }

    static Dictionary<string, string> ParseOptions(string[] args)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Oczekiwano opcji, otrzymano '{args[i]}'.");
            }

            string key = args[i][2..];
            string value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
            options[key] = value;
        }
        return options;
    }

    static string Required(IReadOnlyDictionary<string, string> options, string name) =>
        options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Brak wymaganej opcji --{name}.");

    static IReadOnlyList<string> Columns(IReadOnlyDictionary<string, string> options, string name) =>
        OptionalColumns(options, name) ?? throw new ArgumentException($"Brak wymaganej opcji --{name}.");

    static IReadOnlyList<string>? OptionalColumns(IReadOnlyDictionary<string, string> options, string name) =>
        options.TryGetValue(name, out string? value)
            ? value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : null;

    static T EnumOption<T>(IReadOnlyDictionary<string, string> options, string name, T fallback) where T : struct, Enum =>
        options.TryGetValue(name, out string? value) && Enum.TryParse(value, true, out T parsed)
            ? parsed
            : options.ContainsKey(name) ? throw new ArgumentException($"Nieprawidłowa wartość --{name}.") : fallback;

    static int IntOption(IReadOnlyDictionary<string, string> options, string name, int fallback) =>
        options.TryGetValue(name, out string? value) && int.TryParse(value, out int parsed)
            ? parsed
            : options.ContainsKey(name) ? throw new ArgumentException($"Nieprawidłowa wartość --{name}.") : fallback;

    static char? Delimiter(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("delimiter", out string? value))
        {
            return null;
        }
        return value.ToLowerInvariant() switch
        {
            "tab" or "\\t" => '\t',
            { Length: 1 } => value[0],
            _ => throw new ArgumentException("Separator musi być pojedynczym znakiem albo wartością 'tab'.")
        };
    }

    static IReadOnlyList<string> FindColumns(IEnumerable<CsvImport> imports, string tableName) =>
        imports.FirstOrDefault(item => string.Equals(item.TableName, tableName, StringComparison.OrdinalIgnoreCase))?.Columns.Select(column => column.Name).ToArray()
        ?? throw new ArgumentException($"Tabela '{tableName}' nie jest zarejestrowanym importem; podaj kolumny wynikowe jawnie.");

    static void PrintOperation(OperationResult result)
    {
        Console.WriteLine(result.Message);
        if (!string.IsNullOrWhiteSpace(result.Sql))
        {
            Console.WriteLine(result.Sql);
        }
        else if (!string.IsNullOrWhiteSpace(result.ResultTableName))
        {
            Console.WriteLine($"Tabela wynikowa: {result.ResultTableName}");
        }
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
        CSVForge CLI

        workspace --action create|open --path <workspace.db>
        import --workspace <db> --file <plik.csv> [--name <nazwa>] [--delimiter ;] [--batch-size 5000] [--no-header]
        list-tables --workspace <db>
        duplicates --workspace <db> --table <tabela> --columns <kol1,kol2> [--mode Summary|AllDuplicateRows]
        compare --workspace <db> --left <tabela> --right <tabela> --left-keys <kolumny> --right-keys <kolumny> [--mode AllWithStatus|CommonRows|LeftOnly|RightOnly|DifferentRows]
        join --workspace <db> --left <tabela> --right <tabela> --left-keys <kolumny> --right-keys <kolumny> [--type Inner|Left|Right]
        export --workspace <db> --table <tabela> --output <plik.csv> [--delimiter ;] [--no-header]
        """);
    }
}
