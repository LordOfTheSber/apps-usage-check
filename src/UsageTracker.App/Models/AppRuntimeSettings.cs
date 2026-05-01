namespace UsageTracker.App.Models;

public sealed record AppRuntimeSettings(
    string ConnectionString,
    int PollingIntervalMilliseconds,
    int FlushIntervalSeconds,
    string MinimumLogLevel);
