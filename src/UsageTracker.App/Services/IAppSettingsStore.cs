using UsageTracker.App.Models;

namespace UsageTracker.App.Services;

public interface IAppSettingsStore
{
    AppRuntimeSettings Load();

    void Save(AppRuntimeSettings settings);
}
