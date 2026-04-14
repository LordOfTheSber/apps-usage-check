using System.ComponentModel;
using System.Diagnostics;
using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Core.Services;
using AppsUsageCheck.Infrastructure.Interop;

namespace AppsUsageCheck.Infrastructure.Services;

public sealed class Win32ForegroundDetector : IForegroundDetector
{
    public string? GetForegroundProcessName()
    {
        var foregroundWindowHandle = NativeMethods.GetForegroundWindow();
        if (foregroundWindowHandle == IntPtr.Zero)
        {
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(foregroundWindowHandle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var normalizedProcessName = ProcessNameNormalizer.Normalize(process.ProcessName);
            return normalizedProcessName.Length == 0 ? null : normalizedProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
