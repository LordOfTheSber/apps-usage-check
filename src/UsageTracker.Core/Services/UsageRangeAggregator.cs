using UsageTracker.Core.Models;

namespace UsageTracker.Core.Services;

public static class UsageRangeAggregator
{
    public static IReadOnlyList<UsageRangeContribution> Aggregate(
        IReadOnlyCollection<UsageSession> sessions,
        IReadOnlyCollection<TimeAdjustment> adjustments,
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now)
    {
        if (to <= from)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "The time range end must be after the start.");
        }

        var totals = new Dictionary<Guid, (long Running, long Foreground)>();

        foreach (var session in sessions)
        {
            var effectiveEnd = session.SessionEnd ?? now;
            var duration = effectiveEnd - session.SessionStart;
            if (duration <= TimeSpan.Zero)
            {
                continue;
            }

            var overlapStart = session.SessionStart > from ? session.SessionStart : from;
            var overlapEnd = effectiveEnd < to ? effectiveEnd : to;
            var overlap = overlapEnd - overlapStart;
            if (overlap <= TimeSpan.Zero)
            {
                continue;
            }

            var fraction = overlap.TotalSeconds / duration.TotalSeconds;
            if (fraction > 1.0)
            {
                fraction = 1.0;
            }

            var runningContribution = (long)Math.Round(
                session.TotalRunningSeconds * fraction,
                MidpointRounding.AwayFromZero);
            var foregroundContribution = (long)Math.Round(
                session.ForegroundSeconds * fraction,
                MidpointRounding.AwayFromZero);

            Add(totals, session.TrackedProcessId, runningContribution, foregroundContribution);
        }

        foreach (var adjustment in adjustments)
        {
            if (adjustment.AppliedAt < from || adjustment.AppliedAt >= to)
            {
                continue;
            }

            var running = adjustment.AdjustmentType == TimeAdjustmentTypes.Running
                ? adjustment.AdjustmentSeconds
                : 0L;
            var foreground = adjustment.AdjustmentType == TimeAdjustmentTypes.Foreground
                ? adjustment.AdjustmentSeconds
                : 0L;

            if (running == 0L && foreground == 0L)
            {
                continue;
            }

            Add(totals, adjustment.TrackedProcessId, running, foreground);
        }

        var result = new List<UsageRangeContribution>(totals.Count);
        foreach (var (id, (running, foreground)) in totals)
        {
            result.Add(new UsageRangeContribution(id, running, foreground));
        }

        return result;
    }

    private static void Add(
        Dictionary<Guid, (long Running, long Foreground)> totals,
        Guid trackedProcessId,
        long running,
        long foreground)
    {
        if (totals.TryGetValue(trackedProcessId, out var existing))
        {
            totals[trackedProcessId] = (existing.Running + running, existing.Foreground + foreground);
        }
        else
        {
            totals[trackedProcessId] = (running, foreground);
        }
    }
}
