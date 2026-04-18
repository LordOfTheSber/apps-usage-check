using AppsUsageCheck.App.Models;

namespace AppsUsageCheck.App.Services;

public interface IAppSettingsStore
{
    AppRuntimeSettings Load();

    void Save(AppRuntimeSettings settings);
}
