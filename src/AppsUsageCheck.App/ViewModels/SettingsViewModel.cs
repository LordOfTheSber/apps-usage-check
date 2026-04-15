using AppsUsageCheck.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppsUsageCheck.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAutoStartService _autoStartService;

    [ObservableProperty]
    private bool startWithWindows;

    public SettingsViewModel(IAutoStartService autoStartService)
    {
        _autoStartService = autoStartService ?? throw new ArgumentNullException(nameof(autoStartService));
        StartWithWindows = _autoStartService.IsEnabled();
    }

    public bool TrySave(out string errorMessage)
    {
        try
        {
            _autoStartService.SetEnabled(StartWithWindows);
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            return false;
        }
    }
}
