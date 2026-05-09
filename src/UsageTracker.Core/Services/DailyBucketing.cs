using UsageTracker.Core.Models;

namespace UsageTracker.Core.Services;

public static class DailyBucketing
{
    public static IReadOnlyList<DailyUsageBucket> Bucket(
        IReadOnlyCollection<UsageSession> sessions,
        IReadOnlyCollection<TimeAdjustment> adjustments,
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now,
        TimeZoneInfo timeZone)
    {
        if (to <= from)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "The time range end must be after the start.");
        }

        ArgumentNullException.ThrowIfNull(timeZone);

        var buckets = new List<DailyUsageBucket>();
        var localFrom = TimeZoneInfo.ConvertTime(from, timeZone);
        var currentLocalDate = localFrom.Date;
        var windowStart = from;

        while (windowStart < to)
        {
            var nextLocalMidnight = currentLocalDate.AddDays(1);
            var nextMidnightOffset = ToOffset(nextLocalMidnight, timeZone);
            var windowEnd = nextMidnightOffset < to ? nextMidnightOffset : to;

            if (windowEnd > windowStart)
            {
                var contributions = UsageRangeAggregator.Aggregate(
                    sessions,
                    adjustments,
                    windowStart,
                    windowEnd,
                    now);

                var day = DateOnly.FromDateTime(currentLocalDate);
                foreach (var contribution in contributions)
                {
                    if (contribution.RunningSeconds == 0L && contribution.ForegroundSeconds == 0L)
                    {
                        continue;
                    }

                    buckets.Add(new DailyUsageBucket(
                        day,
                        contribution.TrackedProcessId,
                        contribution.RunningSeconds,
                        contribution.ForegroundSeconds));
                }
            }

            windowStart = windowEnd;
            currentLocalDate = nextLocalMidnight;
        }

        return buckets;
    }

    private static DateTimeOffset ToOffset(DateTime localDateTime, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified));
    }
}
