using CSVForge.Infrastructure;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;
using System.Windows.Threading;
using ApplicationDependencyInjection = CSVForge.Application.DependencyInjection;

namespace CSVForge.App.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        Directory.CreateDirectory(AppPaths.LogsDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(AppPaths.LogsDirectory, "csvforge-wpf-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true)
            .CreateLogger();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                ApplicationDependencyInjection.AddApplication(services);
                services.AddInfrastructure();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        AppPaths.CleanupOrphanedTemporaryFiles();
        await _host.StartAsync();

        MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled WPF dispatcher exception");
        MessageBox.Show(
            "Wystąpił nieoczekiwany błąd. Szczegóły zapisano w logu. Zapisane dane w workspace pozostają bezpieczne; uruchom ponownie aplikację, aby kontynuować pracę.",
            "CSVForge — błąd aplikacji",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e) =>
        Log.Fatal(e.ExceptionObject as Exception, "Unhandled application-domain exception; terminating: {IsTerminating}", e.IsTerminating);

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        await Log.CloseAndFlushAsync();

        base.OnExit(e);
    }
}
