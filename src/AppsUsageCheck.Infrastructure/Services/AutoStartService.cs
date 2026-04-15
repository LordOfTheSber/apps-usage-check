using System.Diagnostics;
using AppsUsageCheck.Core.Interfaces;
using Microsoft.Win32;

namespace AppsUsageCheck.Infrastructure.Services;

public sealed class AutoStartService : IAutoStartService
{
    private const string RunRegistrySubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AppsUsageCheck";
    private const string MinimizedArgument = "--minimized";

    public bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistrySubKey, writable: false);
        return runKey?.GetValue(ValueName) is string command && !string.IsNullOrWhiteSpace(command);
    }

    public void SetEnabled(bool isEnabled)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunRegistrySubKey, writable: true)
            ?? throw new InvalidOperationException("Unable to open the Windows auto-start registry key.");

        if (isEnabled)
        {
            runKey.SetValue(ValueName, BuildRunCommand(GetExecutablePath()), RegistryValueKind.String);
            return;
        }

        runKey.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    internal static string BuildRunCommand(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be empty.", nameof(executablePath));
        }

        return $"\"{executablePath.Trim()}\" {MinimizedArgument}";
    }

    private static string GetExecutablePath()
    {
        var executablePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return executablePath;
        }

        using var process = Process.GetCurrentProcess();
        executablePath = process.MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return executablePath;
        }

        throw new InvalidOperationException("Unable to determine the current executable path for auto-start.");
    }
}
