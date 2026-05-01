using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using UsageTracker.App.Models;
using Microsoft.Extensions.Configuration;

namespace UsageTracker.App.Services;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private const int DefaultPollingIntervalMilliseconds = 1000;
    private const int DefaultFlushIntervalSeconds = 30;
    private const string DefaultMinimumLogLevel = "Information";

    private readonly string _settingsPath;
    private readonly IConfiguration _configuration;

    public JsonAppSettingsStore(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public AppRuntimeSettings Load()
    {
        var connectionString = _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Default' is missing.");
        }

        var pollingIntervalMilliseconds = _configuration.GetValue<int?>("Tracking:PollingIntervalMs")
            ?? DefaultPollingIntervalMilliseconds;
        var flushIntervalSeconds = _configuration.GetValue<int?>("Tracking:FlushIntervalSeconds")
            ?? DefaultFlushIntervalSeconds;
        var minimumLogLevel = _configuration["Logging:MinimumLevel"] ?? DefaultMinimumLogLevel;

        return new AppRuntimeSettings(
            connectionString,
            pollingIntervalMilliseconds,
            flushIntervalSeconds,
            minimumLogLevel);
    }

    public void Save(AppRuntimeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        JsonObject root;
        if (File.Exists(_settingsPath))
        {
            var existingContent = File.ReadAllText(_settingsPath);
            root = JsonNode.Parse(existingContent) as JsonObject
                ?? throw new InvalidOperationException("The appsettings.json file is not a valid JSON object.");
        }
        else
        {
            root = [];
        }

        var connectionStrings = root["ConnectionStrings"] as JsonObject ?? [];
        connectionStrings["Default"] = settings.ConnectionString;
        root["ConnectionStrings"] = connectionStrings;

        var tracking = root["Tracking"] as JsonObject ?? [];
        tracking["PollingIntervalMs"] = settings.PollingIntervalMilliseconds;
        tracking["FlushIntervalSeconds"] = settings.FlushIntervalSeconds;
        root["Tracking"] = tracking;

        var logging = root["Logging"] as JsonObject ?? [];
        logging["MinimumLevel"] = settings.MinimumLogLevel;
        root["Logging"] = logging;

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        File.WriteAllText(_settingsPath, root.ToJsonString(options));
    }
}
