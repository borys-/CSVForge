using CSVForge.Application.Ports;
using CSVForge.Infrastructure.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace CSVForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IWorkspaceService, SqliteWorkspaceService>();

        return services;
    }
}
