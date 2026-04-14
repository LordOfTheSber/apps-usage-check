namespace AppsUsageCheck.Core.Interfaces;

public interface IForegroundDetector
{
    string? GetForegroundProcessName();
}
