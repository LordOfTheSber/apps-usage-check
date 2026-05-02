namespace UsageTracker.Core.Interfaces;

public interface IProcessIconService
{
    string GetIconFilePath(string processName);

    bool IconExists(string processName);

    Task<bool> TryExtractAndSaveAsync(Guid trackedProcessId, string processName, CancellationToken cancellationToken = default);

    Task RefreshMissingForRunningAsync(CancellationToken cancellationToken = default);

    event EventHandler<IconRefreshedEventArgs> IconRefreshed;
}

public sealed record IconRefreshedEventArgs(Guid TrackedProcessId, string ProcessName, DateTimeOffset ExtractedAt);
