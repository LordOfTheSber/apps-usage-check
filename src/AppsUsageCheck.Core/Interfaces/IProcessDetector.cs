namespace AppsUsageCheck.Core.Interfaces;

public interface IProcessDetector
{
    IReadOnlySet<string> GetRunningProcessNames();

    IReadOnlySet<string> GetRunningTargetProcessNames(IEnumerable<string> targetProcessNames);
}
