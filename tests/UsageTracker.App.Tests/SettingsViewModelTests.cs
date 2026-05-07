using UsageTracker.App.Models;
using UsageTracker.App.Services;
using UsageTracker.App.ViewModels;
using UsageTracker.Core.Interfaces;
using Xunit;

namespace UsageTracker.App.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void Constructor_LoadsSettingsAndAutoStartState()
    {
        var settings = new AppRuntimeSettings("Host=localhost", 1500, 45, "Warning");
        var autoStartService = new FakeAutoStartService { Enabled = true };

        var viewModel = new SettingsViewModel(autoStartService, new FakeAppSettingsStore(settings));

        Assert.True(viewModel.StartWithWindows);
        Assert.Equal(1500, viewModel.PollingIntervalMilliseconds);
        Assert.Equal(45, viewModel.FlushIntervalSeconds);
        Assert.Equal("Host=localhost", viewModel.ConnectionString);
        Assert.Equal("Warning", viewModel.MinimumLogLevel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TrySave_NonPositivePollingInterval_ReturnsError(int pollingIntervalMilliseconds)
    {
        var viewModel = CreateViewModel();

        viewModel.PollingIntervalMilliseconds = pollingIntervalMilliseconds;

        var saved = viewModel.TrySave(out var errorMessage, out var infoMessage);

        Assert.False(saved);
        Assert.Equal("Polling interval must be greater than zero.", errorMessage);
        Assert.Null(infoMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TrySave_NonPositiveFlushInterval_ReturnsError(int flushIntervalSeconds)
    {
        var viewModel = CreateViewModel();

        viewModel.FlushIntervalSeconds = flushIntervalSeconds;

        var saved = viewModel.TrySave(out var errorMessage, out var infoMessage);

        Assert.False(saved);
        Assert.Equal("Flush interval must be greater than zero.", errorMessage);
        Assert.Null(infoMessage);
    }

    [Fact]
    public void TrySave_BlankConnectionString_ReturnsError()
    {
        var viewModel = CreateViewModel();

        viewModel.ConnectionString = "   ";

        var saved = viewModel.TrySave(out var errorMessage, out var infoMessage);

        Assert.False(saved);
        Assert.Equal("Connection string cannot be empty.", errorMessage);
        Assert.Null(infoMessage);
    }

    [Fact]
    public void TrySave_UnsupportedLogLevel_ReturnsError()
    {
        var viewModel = CreateViewModel();

        viewModel.MinimumLogLevel = "Trace";

        var saved = viewModel.TrySave(out var errorMessage, out var infoMessage);

        Assert.False(saved);
        Assert.Equal("Select a supported log level.", errorMessage);
        Assert.Null(infoMessage);
    }

    [Fact]
    public void TrySave_SupportedLogLevelCaseInsensitive_SavesCanonicalCasing()
    {
        var store = new FakeAppSettingsStore(DefaultSettings);
        var viewModel = new SettingsViewModel(new FakeAutoStartService(), store)
        {
            MinimumLogLevel = "warning",
        };

        var saved = viewModel.TrySave(out var errorMessage, out _);

        Assert.True(saved);
        Assert.Equal(string.Empty, errorMessage);
        Assert.NotNull(store.SavedSettings);
        Assert.Equal("Warning", store.SavedSettings!.MinimumLogLevel);
    }

    [Fact]
    public void TrySave_ValidSettings_SavesSettingsAndAutoStartFlag()
    {
        var store = new FakeAppSettingsStore(DefaultSettings);
        var autoStartService = new FakeAutoStartService();
        var viewModel = new SettingsViewModel(autoStartService, store)
        {
            StartWithWindows = true,
            ConnectionString = "  Host=changed  ",
            PollingIntervalMilliseconds = 2000,
            FlushIntervalSeconds = 60,
            MinimumLogLevel = "Error",
        };

        var saved = viewModel.TrySave(out var errorMessage, out var infoMessage);

        Assert.True(saved);
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal("Saved. Restart the app for tracking, database, and logging changes to take effect.", infoMessage);
        Assert.NotNull(store.SavedSettings);
        Assert.Equal(new AppRuntimeSettings("Host=changed", 2000, 60, "Error"), store.SavedSettings);
        Assert.True(autoStartService.Enabled);
    }

    [Fact]
    public void TrySave_UnchangedRuntimeSettings_ReturnsNoRestartInfo()
    {
        var viewModel = CreateViewModel();

        var saved = viewModel.TrySave(out var errorMessage, out var infoMessage);

        Assert.True(saved);
        Assert.Equal(string.Empty, errorMessage);
        Assert.Null(infoMessage);
    }

    [Fact]
    public void TrySave_WhenSaveThrows_ReturnsExceptionMessage()
    {
        var store = new FakeAppSettingsStore(DefaultSettings)
        {
            SaveException = new InvalidOperationException("settings file denied"),
        };
        var viewModel = new SettingsViewModel(new FakeAutoStartService(), store);

        var saved = viewModel.TrySave(out var errorMessage, out var infoMessage);

        Assert.False(saved);
        Assert.Equal("settings file denied", errorMessage);
        Assert.Null(infoMessage);
    }

    [Fact]
    public void TrySave_WhenAutoStartThrows_ReturnsExceptionMessage()
    {
        var autoStartService = new FakeAutoStartService
        {
            SetEnabledException = new InvalidOperationException("registry denied"),
        };
        var viewModel = new SettingsViewModel(autoStartService, new FakeAppSettingsStore(DefaultSettings));

        var saved = viewModel.TrySave(out var errorMessage, out var infoMessage);

        Assert.False(saved);
        Assert.Equal("registry denied", errorMessage);
        Assert.Null(infoMessage);
    }

    private static readonly AppRuntimeSettings DefaultSettings = new(
        "Host=localhost",
        1000,
        30,
        "Information");

    private static SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(
            new FakeAutoStartService(),
            new FakeAppSettingsStore(DefaultSettings));
    }

    private sealed class FakeAutoStartService : IAutoStartService
    {
        public bool Enabled { get; set; }

        public Exception? SetEnabledException { get; set; }

        public bool IsEnabled() => Enabled;

        public void SetEnabled(bool isEnabled)
        {
            if (SetEnabledException is not null)
            {
                throw SetEnabledException;
            }

            Enabled = isEnabled;
        }
    }

    private sealed class FakeAppSettingsStore : IAppSettingsStore
    {
        private readonly AppRuntimeSettings _settings;

        public FakeAppSettingsStore(AppRuntimeSettings settings)
        {
            _settings = settings;
        }

        public AppRuntimeSettings? SavedSettings { get; private set; }

        public Exception? SaveException { get; set; }

        public AppRuntimeSettings Load() => _settings;

        public void Save(AppRuntimeSettings settings)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            SavedSettings = settings;
        }
    }
}
