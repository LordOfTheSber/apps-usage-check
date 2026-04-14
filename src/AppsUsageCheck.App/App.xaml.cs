using System.Windows;
using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Core.Services;
using AppsUsageCheck.Infrastructure;
using AppsUsageCheck.Infrastructure.Data;
using AppsUsageCheck.App.Services;
using AppsUsageCheck.App.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AppsUsageCheck.App;

public partial class App : Application
{
    private IHost? _host;
    private ITrackingEngine? _trackingEngine;
    private ITrayIconService? _trayIconService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = CreateHostBuilder(e.Args).Build();
            await ApplyDatabaseMigrationsAsync(_host.Services).ConfigureAwait(true);
            await _host.StartAsync().ConfigureAwait(true);
            _trackingEngine = _host.Services.GetRequiredService<ITrackingEngine>();
            await _trackingEngine.StartAsync().ConfigureAwait(true);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            MainWindow = _host.Services.GetRequiredService<MainWindow>();

            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
            await mainViewModel.InitializeAsync().ConfigureAwait(true);

            _trayIconService = _host.Services.GetRequiredService<ITrayIconService>();
            _trayIconService.Initialize();

            if (ShouldStartMinimized(e.Args))
            {
                return;
            }

            MainWindow.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Application startup failed.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "AppsUsageCheck",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_trayIconService is IDisposable disposableTrayIconService)
        {
            disposableTrayIconService.Dispose();
            _trayIconService = null;
        }

        if (_trackingEngine is not null)
        {
            await _trackingEngine.StopAsync().ConfigureAwait(true);
        }

        if (_host is not null)
        {
            try
            {
                await _host.StopAsync().ConfigureAwait(true);
            }
            finally
            {
                _host.Dispose();
            }
        }

        base.OnExit(e);
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration(
                (_, configurationBuilder) =>
                {
                    configurationBuilder.Sources.Clear();
                    configurationBuilder
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                        .AddEnvironmentVariables(prefix: "APPS_USAGE_CHECK_");
                })
            .ConfigureServices(
                (context, services) =>
                {
                    var connectionString = context.Configuration.GetConnectionString("Default");
                    if (string.IsNullOrWhiteSpace(connectionString))
                    {
                        throw new InvalidOperationException("Connection string 'Default' is missing.");
                    }

                    services.AddSingleton(TimeProvider.System);
                    services.AddInfrastructure(connectionString);
                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<ITrayIconService, TrayIconService>();
                    services.AddSingleton<ITrackingEngine>(
                        serviceProvider =>
                        {
                            var pollingIntervalMilliseconds = context.Configuration.GetValue<int?>("Tracking:PollingIntervalMs") ?? 1000;
                            var flushIntervalSeconds = context.Configuration.GetValue<int?>("Tracking:FlushIntervalSeconds") ?? 30;

                            return new TrackingEngine(
                                serviceProvider.GetRequiredService<IProcessDetector>(),
                                serviceProvider.GetRequiredService<IForegroundDetector>(),
                                serviceProvider.GetRequiredService<IUsageRepository>(),
                                TimeSpan.FromMilliseconds(pollingIntervalMilliseconds),
                                TimeSpan.FromSeconds(flushIntervalSeconds),
                                serviceProvider.GetRequiredService<TimeProvider>());
                        });
                });
    }

    private static async Task ApplyDatabaseMigrationsAsync(IServiceProvider services)
    {
        var dbContextFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    private static bool ShouldStartMinimized(IEnumerable<string> args)
    {
        return args.Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));
    }
}
