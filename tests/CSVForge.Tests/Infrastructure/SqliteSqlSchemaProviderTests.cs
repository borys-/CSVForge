using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Sql;
using CSVForge.Domain.Imports;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteSqlSchemaProviderTests
{
    [Fact]
    public async Task GetSqlSchemaUseCase_ReflectsCreateAlterAndDropWithoutRestart()
    {
        string workspacePath = Path.Combine(
            Path.GetTempPath(),
            "CSVForge.Tests",
            Guid.NewGuid().ToString("N"),
            "workspace.db");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        IExecuteSqlUseCase executeSql = provider.GetRequiredService<IExecuteSqlUseCase>();
        IGetSqlSchemaUseCase getSchema = provider.GetRequiredService<IGetSqlSchemaUseCase>();

        await executeSql.ExecuteAsync("CREATE TABLE products (id INTEGER, name TEXT); CREATE VIEW product_names AS SELECT name FROM products;");
        SqlSchemaSnapshot created = await getSchema.ExecuteAsync();
        Assert.Contains(created.Objects, item =>
            item.Name == "products" && item.Kind == SqlSchemaObjectKind.Table && item.Columns.SequenceEqual(["id", "name"]));
        Assert.Contains(created.Objects, item =>
            item.Name == "product_names" && item.Kind == SqlSchemaObjectKind.View && item.Columns.SequenceEqual(["name"]));

        await executeSql.ExecuteAsync("ALTER TABLE products ADD COLUMN price REAL;");
        SqlSchemaSnapshot altered = await getSchema.ExecuteAsync();
        Assert.Contains(altered.Objects.Single(item => item.Name == "products").Columns, column => column == "price");

        await executeSql.ExecuteAsync("DROP VIEW product_names; DROP TABLE products;");
        SqlSchemaSnapshot dropped = await getSchema.ExecuteAsync();
        Assert.DoesNotContain(dropped.Objects, item => item.Name is "products" or "product_names");
    }

    [Fact]
    public async Task GetSqlSchemaUseCase_ReflectsCompletedCsvImport()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CSVForge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string workspacePath = Path.Combine(directory, "workspace.db");
        string csvPath = Path.Combine(directory, "people.csv");
        await File.WriteAllTextAsync(csvPath, "Id;Full Name\r\n1;Ada\r\n");
        ServiceProvider provider = new ServiceCollection().AddApplication().AddInfrastructure().BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);

        ImportResult import = await provider.GetRequiredService<IImportCsvUseCase>()
            .ExecuteAsync(new ImportRequest(csvPath, "People", true, null, null));
        SqlSchemaSnapshot schema = await provider.GetRequiredService<IGetSqlSchemaUseCase>().ExecuteAsync();

        Assert.Contains(schema.Objects, item =>
            item.Name == import.Import.TableName && item.Columns.SequenceEqual(["Id", "Full_Name"]));
    }
}
