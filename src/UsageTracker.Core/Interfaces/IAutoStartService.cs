namespace UsageTracker.Core.Interfaces;

public interface IAutoStartService
{
    bool IsEnabled();

    void SetEnabled(bool isEnabled);
}
