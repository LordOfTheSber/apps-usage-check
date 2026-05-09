namespace UsageTracker.Core.Models;

public sealed record DailyUsageBucket(
    DateOnly Day,
    Guid TrackedProcessId,
    long RunningSeconds,
    long ForegroundSeconds);
