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
}
