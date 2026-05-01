using System.IO;
using System.Windows;
using UsageTracker.Core.Interfaces;
using UsageTracker.Core.Services;
using UsageTracker.Infrastructure;
using UsageTracker.Infrastructure.Data;
using UsageTracker.Infrastructure.Services;
using UsageTracker.App.Services;
using UsageTracker.App.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace UsageTracker.App;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\UsageTracker";
    private const string LegacySingleInstanceMutexName = @"Local\AppsUsageCheck";
    private const string AppDisplayName = "Usage Tracker";
    private IHost? _host;
    private ITrackingEngine? _trackingEngine;
    private ITrayIconService? _trayIconService;
    private IDatabaseHealthCheck? _databaseHealthCheck;
    private Mutex? _singleInstanceMutex;
    private Mutex? _legacySingleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private bool _ownsLegacySingleInstanceMutex;
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
            _host.Services.GetRequiredService<ILogger<App>>().LogInformation("Usage Tracker started.");

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
                AppDisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            _host?.Services.GetService<ILogger<App>>()?.LogInformation("Usage Tracker is shutting down.");

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
                        .AddEnvironmentVariables(prefix: "USAGE_TRACKER_");
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
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            ShowAlreadyRunningMessage(args);
            return false;
        }

        _ownsSingleInstanceMutex = true;

        _legacySingleInstanceMutex = new Mutex(initiallyOwned: true, LegacySingleInstanceMutexName, out var legacyCreatedNew);
        if (legacyCreatedNew)
        {
            _ownsLegacySingleInstanceMutex = true;
            return true;
        }

        _legacySingleInstanceMutex.Dispose();
        _legacySingleInstanceMutex = null;
        ReleaseSingleInstanceMutex();
        ShowAlreadyRunningMessage(args);
        return false;
    }

    private static void ShowAlreadyRunningMessage(IEnumerable<string> args)
    {
        if (ShouldStartMinimized(args))
        {
            return;
        }

        MessageBox.Show(
            "Usage Tracker is already running in the system tray.",
            AppDisplayName,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ReleaseSingleInstanceMutex()
    {
        ReleaseMutex(ref _singleInstanceMutex, ref _ownsSingleInstanceMutex);
        ReleaseMutex(ref _legacySingleInstanceMutex, ref _ownsLegacySingleInstanceMutex);
    }

    private static void ReleaseMutex(ref Mutex? mutex, ref bool ownsMutex)
    {
        if (mutex is null)
        {
            return;
        }

        try
        {
            if (ownsMutex)
            {
                mutex.ReleaseMutex();
            }
        }
        catch (ApplicationException)
        {
        }
        finally
        {
            mutex.Dispose();
            mutex = null;
            ownsMutex = false;
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
            AppDisplayName,
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
