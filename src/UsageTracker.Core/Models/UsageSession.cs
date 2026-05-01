namespace UsageTracker.Core.Models;

public sealed class UsageSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TrackedProcessId { get; set; }

    public DateTimeOffset SessionStart { get; set; }

    public DateTimeOffset? SessionEnd { get; set; }

    public long TotalRunningSeconds { get; set; }

    public long ForegroundSeconds { get; set; }

    public bool IsManualEdit { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
