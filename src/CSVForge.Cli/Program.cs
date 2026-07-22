using CSVForge.Application;
using CSVForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/csvforge-cli-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    using IHost host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices(services =>
        {
            services.AddApplication();
            services.AddInfrastructure();
        })
        .Build();

    ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
    IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

    logger.LogInformation("CSVForge CLI started.");
    Console.WriteLine("CSVForge CLI");
    lifetime.StopApplication();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CSVForge CLI stopped unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
