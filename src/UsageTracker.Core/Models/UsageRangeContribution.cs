namespace UsageTracker.Core.Models;

public sealed record UsageRangeContribution(
    Guid TrackedProcessId,
    long RunningSeconds,
    long ForegroundSeconds);
