namespace AppsUsageCheck.Core.Models;

public sealed class TimeAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TrackedProcessId { get; set; }

    public string AdjustmentType { get; set; } = string.Empty;

    public long AdjustmentSeconds { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;
}
