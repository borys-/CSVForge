using CSVForge.Application;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddApplication();
        services.AddInfrastructure();
    })
    .Build();

IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

Console.WriteLine("CSVForge CLI");
lifetime.StopApplication();
