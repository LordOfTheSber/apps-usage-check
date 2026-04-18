using System.Diagnostics;
using System.ComponentModel;
using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Core.Services;
using Microsoft.Extensions.Logging;

namespace AppsUsageCheck.Infrastructure.Services;

public sealed class Win32ProcessDetector : IProcessDetector
{
    private readonly ILogger<Win32ProcessDetector> _logger;

    public Win32ProcessDetector(ILogger<Win32ProcessDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlySet<string> GetRunningProcessNames()
    {
        var processNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.SessionId == 0)
                {
                    continue;
                }

                var normalizedProcessName = ProcessNameNormalizer.Normalize(process.ProcessName);
                if (normalizedProcessName.Length > 0)
                {
                    processNames.Add(normalizedProcessName);
                }
            }
            catch (Win32Exception exception)
            {
                _logger.LogWarning(exception, "Unable to inspect process {ProcessId}.", process.Id);
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
