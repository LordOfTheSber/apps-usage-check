using UsageTracker.App.Models;
using UsageTracker.App.Services;
using UsageTracker.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UsageTracker.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly string[] SupportedLogLevels =
    [
        "Verbose",
        "Debug",
        "Information",
        "Warning",
        "Error",
        "Fatal",
    ];

    private readonly IAutoStartService _autoStartService;
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly AppRuntimeSettings _initialSettings;

    [ObservableProperty]
    private bool startWithWindows;

    [ObservableProperty]
    private int pollingIntervalMilliseconds;

    [ObservableProperty]
    private int flushIntervalSeconds;

    [ObservableProperty]
    private string connectionString = string.Empty;

    [ObservableProperty]
    private string minimumLogLevel = "Information";

    public SettingsViewModel(
        IAutoStartService autoStartService,
        IAppSettingsStore appSettingsStore)
    {
        _autoStartService = autoStartService ?? throw new ArgumentNullException(nameof(autoStartService));
        _appSettingsStore = appSettingsStore ?? throw new ArgumentNullException(nameof(appSettingsStore));
        _initialSettings = _appSettingsStore.Load();
        StartWithWindows = _autoStartService.IsEnabled();
        PollingIntervalMilliseconds = _initialSettings.PollingIntervalMilliseconds;
        FlushIntervalSeconds = _initialSettings.FlushIntervalSeconds;
        ConnectionString = _initialSettings.ConnectionString;
        MinimumLogLevel = _initialSettings.MinimumLogLevel;
    }

    public IReadOnlyList<string> AvailableLogLevels => SupportedLogLevels;

    public bool TrySave(out string errorMessage, out string? infoMessage)
    {
        var normalizedConnectionString = ConnectionString.Trim();
        var normalizedMinimumLogLevel = MinimumLogLevel.Trim();

        if (PollingIntervalMilliseconds <= 0)
        {
            errorMessage = "Polling interval must be greater than zero.";
            infoMessage = null;
            return false;
        }

        if (FlushIntervalSeconds <= 0)
        {
            errorMessage = "Flush interval must be greater than zero.";
            infoMessage = null;
            return false;
        }

        if (normalizedConnectionString.Length == 0)
        {
            errorMessage = "Connection string cannot be empty.";
            infoMessage = null;
            return false;
        }

        if (!SupportedLogLevels.Contains(normalizedMinimumLogLevel, StringComparer.OrdinalIgnoreCase))
        {
            errorMessage = "Select a supported log level.";
            infoMessage = null;
            return false;
        }

        var newSettings = new AppRuntimeSettings(
            normalizedConnectionString,
            PollingIntervalMilliseconds,
            FlushIntervalSeconds,
            SupportedLogLevels.First(level => string.Equals(level, normalizedMinimumLogLevel, StringComparison.OrdinalIgnoreCase)));

        try
        {
            _appSettingsStore.Save(newSettings);
            _autoStartService.SetEnabled(StartWithWindows);
            errorMessage = string.Empty;
            infoMessage = newSettings != _initialSettings
                ? "Saved. Restart the app for tracking, database, and logging changes to take effect."
                : null;
            return true;
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            infoMessage = null;
            return false;
        }
    }
}
