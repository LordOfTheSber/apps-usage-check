namespace AppsUsageCheck.Core.Interfaces;

public interface IAutoStartService
{
    bool IsEnabled();

    void SetEnabled(bool isEnabled);
}
