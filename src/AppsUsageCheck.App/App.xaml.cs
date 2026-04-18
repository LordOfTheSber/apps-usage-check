using System.IO;
using System.Windows;
using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Core.Services;
using AppsUsageCheck.Infrastructure;
using AppsUsageCheck.Infrastructure.Data;
using AppsUsageCheck.Infrastructure.Services;
using AppsUsageCheck.App.Services;
using AppsUsageCheck.App.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace AppsUsageCheck.App;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\AppsUsageCheck";
    private IHost? _host;
    private ITrackingEngine? _trackingEngine;
    private ITrayIconService? _trayIconService;
    private IDatabaseHealthCheck? _databaseHealthCheck;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private bool _globalExceptionHandlersRegistered;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!TryAcquireSingleInstance(e.Args))
        {
            Shutdown(0);
            return;
        }

        try
        {
            _host = CreateHostBuilder(e.Args).Build();
            RegisterGlobalExceptionHandlers();
            await ApplyDatabaseMigrationsAsync(_host.Services).ConfigureAwait(true);
            await _host.StartAsync().ConfigureAwait(true);
            _databaseHealthCheck = _host.Services.GetRequiredService<IDatabaseHealthCheck>();
            await _databaseHealthCheck.StartAsync().ConfigureAwait(true);
            _trackingEngine = _host.Services.GetRequiredService<ITrackingEngine>();
            await _trackingEngine.StartAsync().ConfigureAwait(true);
            _host.Services.GetRequiredService<ILogger<App>>().LogInformation("Apps Usage Check started.");

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
            Log.Fatal(exception, "Application startup failed.");
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
        try
        {
            _host?.Services.GetService<ILogger<App>>()?.LogInformation("Apps Usage Check is shutting down.");

            if (_trayIconService is IDisposable disposableTrayIconService)
            {
                disposableTrayIconService.Dispose();
                _trayIconService = null;
            }

            if (_trackingEngine is not null)
            {
                await _trackingEngine.StopAsync().ConfigureAwait(true);
            }

            if (_databaseHealthCheck is not null)
            {
                await _databaseHealthCheck.StopAsync().ConfigureAwait(true);
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
        }
        finally
        {
            UnregisterGlobalExceptionHandlers();
            ReleaseSingleInstanceMutex();
            Log.CloseAndFlush();
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
            .UseSerilog(
                (context, _, loggerConfiguration) =>
                {
                    ConfigureSerilog(context.Configuration, loggerConfiguration);
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
                    services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<ITrayIconService, TrayIconService>();
                    services.AddSingleton<ITrackingEngine>(
                        serviceProvider =>
                        {
                            var pollingIntervalMilliseconds = context.Configuration.GetValue<int?>("Tracking:PollingIntervalMs") ?? 1000;
                            var flushIntervalSeconds = context.Configuration.GetValue<int?>("Tracking:FlushIntervalSeconds") ?? 30;
                            var logger = serviceProvider.GetRequiredService<ILogger<TrackingEngine>>();

                            return new TrackingEngine(
                                serviceProvider.GetRequiredService<IProcessDetector>(),
                                serviceProvider.GetRequiredService<IForegroundDetector>(),
                                serviceProvider.GetRequiredService<IUsageRepository>(),
                                TimeSpan.FromMilliseconds(pollingIntervalMilliseconds),
                                TimeSpan.FromSeconds(flushIntervalSeconds),
                                serviceProvider.GetRequiredService<TimeProvider>(),
                                errorHandler: exception => logger.LogError(exception, "Tracking tick failed."));
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

    private static void ConfigureSerilog(IConfiguration configuration, LoggerConfiguration loggerConfiguration)
    {
        var minimumLevel = ParseMinimumLevel(configuration["Logging:MinimumLevel"]);
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        loggerConfiguration
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDirectory, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10 * 1024 * 1024);
    }

    private static LogEventLevel ParseMinimumLevel(string? configuredLevel)
    {
        return configuredLevel?.Trim().ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }

    private bool TryAcquireSingleInstance(IEnumerable<string> args)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (createdNew)
        {
            _ownsSingleInstanceMutex = true;
            return true;
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;

        if (!ShouldStartMinimized(args))
        {
            MessageBox.Show(
                "Apps Usage Check is already running in the system tray.",
                "AppsUsageCheck",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return false;
    }

    private void ReleaseSingleInstanceMutex()
    {
        if (_singleInstanceMutex is null)
        {
            return;
        }

        try
        {
            if (_ownsSingleInstanceMutex)
            {
                _singleInstanceMutex.ReleaseMutex();
            }
        }
        catch (ApplicationException)
        {
        }
        finally
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            _ownsSingleInstanceMutex = false;
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        if (_globalExceptionHandlersRegistered)
        {
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _globalExceptionHandlersRegistered = true;
    }

    private void UnregisterGlobalExceptionHandlers()
    {
        if (!_globalExceptionHandlersRegistered)
        {
            return;
        }

        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _globalExceptionHandlersRegistered = false;
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled dispatcher exception.");
        MessageBox.Show(
            $"An unexpected error occurred.{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}",
            "AppsUsageCheck",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Log.Fatal(exception, "Unhandled application exception.");
        }
        else
        {
            Log.Fatal("Unhandled application exception of unknown type.");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception.");
        e.SetObserved();
    }
}
