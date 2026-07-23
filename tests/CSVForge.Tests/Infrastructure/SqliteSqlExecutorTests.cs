using CSVForge.Application;
using CSVForge.Application.Abstractions;
using CSVForge.Application.Sql;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Tests.Infrastructure;

public sealed class SqliteSqlExecutorTests
{
    [Fact]
    public async Task ExecuteSqlUseCase_ReturnsScrollableQueryResult()
    {
        ServiceProvider provider = await CreateProviderAsync();

        SqlQueryResult result = await provider.GetRequiredService<IExecuteSqlUseCase>()
            .ExecuteAsync("SELECT 1 AS Id, 'Ada' AS Name UNION ALL SELECT 2, 'Ola';");

        Assert.Equal(["Id", "Name"], result.Columns);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("1", result.Rows[0]["Id"]);
        Assert.Equal("Ola", result.Rows[1]["Name"]);
        Assert.False(result.WasTruncated);
    }

    [Fact]
    public async Task ExecuteSqlUseCase_ExecutesStatementsWithoutResultSet()
    {
        ServiceProvider provider = await CreateProviderAsync();
        IExecuteSqlUseCase executeSql = provider.GetRequiredService<IExecuteSqlUseCase>();

        await executeSql.ExecuteAsync("CREATE TABLE custom_data (value TEXT);");
        SqlQueryResult insert = await executeSql.ExecuteAsync("INSERT INTO custom_data (value) VALUES ('test');");
        SqlQueryResult query = await executeSql.ExecuteAsync("SELECT value FROM custom_data;");

        Assert.Equal(1, insert.AffectedRows);
        Assert.Equal("test", query.Rows.Single()["value"]);
    }

    private static async Task<ServiceProvider> CreateProviderAsync()
    {
        string workspacePath = Path.Combine(
            Path.GetTempPath(),
            "CSVForge.Tests",
            Guid.NewGuid().ToString("N"),
            "workspace.db");
        ServiceProvider provider = new ServiceCollection()
            .AddApplication()
            .AddInfrastructure()
            .BuildServiceProvider();
        await provider.GetRequiredService<ICreateWorkspaceUseCase>().ExecuteAsync(workspacePath);
        return provider;
    }
}
