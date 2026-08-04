using CSVForge.Application.Ports;
using CSVForge.Infrastructure.Csv;
using CSVForge.Infrastructure.Operations;
using CSVForge.Infrastructure.Sqlite;
using CSVForge.Infrastructure.Tables;
using CSVForge.Infrastructure.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IWorkspaceContext, WorkspaceContext>();
        services.AddSingleton<IWorkspaceService, SqliteWorkspaceService>();
        services.AddSingleton<IWorkspaceMaintenanceService, SqliteWorkspaceMaintenanceService>();
        services.AddSingleton<ICsvReader, CsvReaderService>();
        services.AddSingleton<ICsvImporter, CsvImporterService>();
        services.AddSingleton<ICsvStagingService, SqliteCsvStagingService>();
        services.AddSingleton<ITableBrowser, SqliteTableBrowser>();
        services.AddSingleton<IDuplicateFinder, SqliteDuplicateFinder>();
        services.AddSingleton<IDatasetComparer, SqliteDatasetComparer>();
        services.AddSingleton<IDatasetJoiner, SqliteDatasetJoiner>();
        services.AddSingleton<ITableExporter, SqliteTableExporter>();
        services.AddSingleton<ITableMaterializer, SqliteTableMaterializer>();
        services.AddSingleton<IOperationHistory, SqliteOperationHistory>();
        services.AddSingleton<ISqlExecutor, SqliteSqlExecutor>();
        services.AddSingleton<ISqlSchemaProvider, SqliteSqlSchemaProvider>();

        return services;
    }
}
