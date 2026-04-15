using System.Diagnostics;
using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Core.Services;

namespace AppsUsageCheck.Infrastructure.Services;

public sealed class Win32ProcessDetector : IProcessDetector
{
    public IReadOnlySet<string> GetRunningProcessNames()
    {
        var processNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var normalizedProcessName = ProcessNameNormalizer.Normalize(process.ProcessName);
                if (normalizedProcessName.Length > 0)
                {
                    processNames.Add(normalizedProcessName);
                }
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return processNames;
    }

    public IReadOnlySet<string> GetRunningTargetProcessNames(IEnumerable<string> targetProcessNames)
    {
        ArgumentNullException.ThrowIfNull(targetProcessNames);

        var normalizedTargetNames = NormalizeProcessNames(targetProcessNames);
        var runningTargetNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var normalizedTargetName in normalizedTargetNames)
        {
            var processes = Process.GetProcessesByName(normalizedTargetName);

            try
            {
                if (processes.Length > 0)
                {
                    runningTargetNames.Add(normalizedTargetName);
                }
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        return runningTargetNames;
    }

    private static HashSet<string> NormalizeProcessNames(IEnumerable<string> processNames)
    {
        var normalizedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var processName in processNames)
        {
            var normalizedProcessName = ProcessNameNormalizer.Normalize(processName);
            if (normalizedProcessName.Length > 0)
            {
                normalizedNames.Add(normalizedProcessName);
            }
        }

        return normalizedNames;
    }
}
