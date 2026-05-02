using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using UsageTracker.Core.Interfaces;
using UsageTracker.Core.Services;
using Microsoft.Extensions.Logging;

namespace UsageTracker.Infrastructure.Services;

[SupportedOSPlatform("windows")]
public sealed class Win32ProcessIconService : IProcessIconService
{
    private readonly IProcessDetector _processDetector;
    private readonly IUsageRepository _usageRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<Win32ProcessIconService> _logger;
    private readonly string _iconDirectory;

    public Win32ProcessIconService(
        IProcessDetector processDetector,
        IUsageRepository usageRepository,
        TimeProvider timeProvider,
        ILogger<Win32ProcessIconService> logger)
    {
        _processDetector = processDetector ?? throw new ArgumentNullException(nameof(processDetector));
        _usageRepository = usageRepository ?? throw new ArgumentNullException(nameof(usageRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _iconDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UsageTracker",
            "icons");
        Directory.CreateDirectory(_iconDirectory);
    }

    public event EventHandler<IconRefreshedEventArgs>? IconRefreshed;

    public string GetIconFilePath(string processName)
    {
        ArgumentNullException.ThrowIfNull(processName);
        return Path.Combine(_iconDirectory, SanitizeFileName(processName) + ".png");
    }

    public bool IconExists(string processName)
    {
        return File.Exists(GetIconFilePath(processName));
    }

    public async Task<bool> TryExtractAndSaveAsync(Guid trackedProcessId, string processName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processName);

        var normalized = ProcessNameNormalizer.Normalize(processName);
        if (normalized.Length == 0)
        {
            return false;
        }

        var exePath = TryGetRunningExecutablePath(normalized);
        if (exePath is null)
        {
            return false;
        }

        _logger.LogInformation("Extracting icon for {ProcessName} from {ExePath}.", normalized, exePath);

        var finalPath = GetIconFilePath(normalized);

        try
        {
            await Task.Run(() => ExtractAndWritePng(exePath, finalPath), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to extract icon for {ProcessName} from {ExePath}.", normalized, exePath);
            return false;
        }

        var extractedAt = _timeProvider.GetUtcNow();
        await _usageRepository.UpdateIconExtractedAtAsync(trackedProcessId, extractedAt, cancellationToken).ConfigureAwait(false);

        IconRefreshed?.Invoke(this, new IconRefreshedEventArgs(trackedProcessId, normalized, extractedAt));
        return true;
    }

    public async Task RefreshMissingForRunningAsync(CancellationToken cancellationToken = default)
    {
        var trackedProcesses = await _usageRepository.GetTrackedProcessesAsync(cancellationToken).ConfigureAwait(false);
        var needsRefresh = trackedProcesses
            .Where(process => process.IconExtractedAt is null || !IconExists(process.ProcessName))
            .ToArray();

        if (needsRefresh.Length == 0)
        {
            return;
        }

        var runningNames = _processDetector.GetRunningProcessNames();

        foreach (var process in needsRefresh)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!runningNames.Contains(process.ProcessName))
            {
                continue;
            }

            await TryExtractAndSaveAsync(process.Id, process.ProcessName, cancellationToken).ConfigureAwait(false);
        }
    }

    private string? TryGetRunningExecutablePath(string normalizedProcessName)
    {
        var processes = Process.GetProcessesByName(normalizedProcessName);
        if (processes.Length == 0)
        {
            _logger.LogWarning(
                "No running instance found for {ProcessName} (Process.GetProcessesByName returned 0).",
                normalizedProcessName);
            return null;
        }

        try
        {
            Exception? lastError = null;
            foreach (var process in processes)
            {
                int processId;
                try
                {
                    processId = process.Id;
                }
                catch (Exception exception)
                {
                    lastError = exception;
                    continue;
                }

                var path = TryGetExecutablePathViaQueryFullProcessImageName(processId, out var queryError);
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }

                if (queryError is not null)
                {
                    lastError = new Win32Exception(queryError.Value);
                }

                // Fallback: MainModule. Often denied for cross-bitness or VM-read-restricted processes,
                // but cheap to try if QueryFullProcessImageName failed for an unexpected reason.
                try
                {
                    var modulePath = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(modulePath))
                    {
                        return modulePath;
                    }
                }
                catch (Exception exception)
                {
                    lastError = exception;
                }
            }

            _logger.LogWarning(
                lastError,
                "Found {Count} running instance(s) of {ProcessName} but could not read the executable path. " +
                "This usually means the target process denies PROCESS_QUERY_LIMITED_INFORMATION to the current user " +
                "(protected/elevated process). Run Usage Tracker elevated if you want icons for those processes.",
                processes.Length,
                normalizedProcessName);
            return null;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static string? TryGetExecutablePathViaQueryFullProcessImageName(int processId, out int? lastWin32Error)
    {
        lastWin32Error = null;
        var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);
        if (handle == IntPtr.Zero)
        {
            lastWin32Error = Marshal.GetLastWin32Error();
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = (uint)buffer.Capacity;
            if (NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size))
            {
                return buffer.ToString(0, (int)size);
            }

            lastWin32Error = Marshal.GetLastWin32Error();
            return null;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    private static void ExtractAndWritePng(string exePath, string finalPath)
    {
        using var icon = Icon.ExtractAssociatedIcon(exePath);
        if (icon is null)
        {
            throw new InvalidOperationException("ExtractAssociatedIcon returned null.");
        }

        using var bmp = icon.ToBitmap();
        using var resized = new Bitmap(bmp, 32, 32);

        var tempPath = finalPath + ".tmp";
        resized.Save(tempPath, ImageFormat.Png);
        File.Move(tempPath, finalPath, overwrite: true);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            builder.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        }

        return builder.ToString();
    }

    private static class NativeMethods
    {
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder exeName, ref uint size);
    }
}
