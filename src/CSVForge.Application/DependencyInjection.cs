using CSVForge.Application.Abstractions;
using CSVForge.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICreateWorkspaceUseCase, CreateWorkspaceUseCase>();
        services.AddScoped<IOpenWorkspaceUseCase, OpenWorkspaceUseCase>();
        services.AddScoped<IImportCsvUseCase, ImportCsvUseCase>();
        services.AddScoped<IPreviewCsvUseCase, PreviewCsvUseCase>();
        services.AddScoped<IListImportedTablesUseCase, ListImportedTablesUseCase>();
        services.AddScoped<IBrowseTableUseCase, BrowseTableUseCase>();
        services.AddScoped<IFindDuplicatesUseCase, FindDuplicatesUseCase>();
        services.AddScoped<ICompareDatasetsUseCase, CompareDatasetsUseCase>();
        services.AddScoped<IJoinDatasetsUseCase, JoinDatasetsUseCase>();
        services.AddScoped<IExportTableUseCase, ExportTableUseCase>();

        return services;
    }
}
