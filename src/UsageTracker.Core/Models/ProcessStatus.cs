using UsageTracker.Core.Enums;

namespace UsageTracker.Core.Models;

public sealed class ProcessStatus
{
    public Guid TrackedProcessId { get; set; }

    public string ProcessName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public TrackingState TrackingState { get; set; }

    public bool IsRunning { get; set; }

    public bool IsForeground { get; set; }

    public long TotalRunningSeconds { get; set; }

    public long ForegroundSeconds { get; set; }

    public long CurrentSessionRunningSeconds { get; set; }

    public long CurrentSessionForegroundSeconds { get; set; }

    public DateTimeOffset? CurrentSessionStart { get; set; }
}
