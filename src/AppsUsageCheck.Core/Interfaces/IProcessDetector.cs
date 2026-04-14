namespace AppsUsageCheck.Core.Interfaces;

public interface IProcessDetector
{
    IReadOnlySet<string> GetRunningProcessNames();
}
