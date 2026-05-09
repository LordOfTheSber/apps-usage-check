using UsageTracker.Core.Models;
using UsageTracker.Core.Services;
using Xunit;

namespace UsageTracker.Core.Tests;

public sealed class DailyBucketingTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly TimeZoneInfo PlusThree = TimeZoneInfo.CreateCustomTimeZone(
        id: "UTC+03",
        baseUtcOffset: TimeSpan.FromHours(3),
        displayName: "UTC+03",
        standardDisplayName: "UTC+03");

    [Fact]
    public void Bucket_EmptyInputs_ReturnsEmpty()
    {
        var from = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);

        var result = DailyBucketing.Bucket(
            Array.Empty<UsageSession>(),
            Array.Empty<TimeAdjustment>(),
            from,
            to,
            from,
            Utc);

        Assert.Empty(result);
    }

    [Fact]
    public void Bucket_EndNotAfterStart_Throws()
    {
        var instant = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => DailyBucketing.Bucket(
            Array.Empty<UsageSession>(),
            Array.Empty<TimeAdjustment>(),
            instant,
            instant,
            instant,
            Utc));
    }

    [Fact]
    public void Bucket_SessionInsideOneDay_ProducesOneBucket()
    {
        var processId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);
        var session = MakeSession(processId, from.AddHours(2), from.AddHours(3), 3600, 1800);

        var result = DailyBucketing.Bucket(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            from,
            to,
            to,
            Utc);

        var bucket = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 5, 7), bucket.Day);
        Assert.Equal(processId, bucket.TrackedProcessId);
        Assert.Equal(3600L, bucket.RunningSeconds);
        Assert.Equal(1800L, bucket.ForegroundSeconds);
    }

    [Fact]
    public void Bucket_ThreeDayRange_OneSessionPerDay_ProducesThreeBuckets()
    {
        var processId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero);

        var sessions = new[]
        {
            MakeSession(processId, from.AddDays(0).AddHours(10), from.AddDays(0).AddHours(11), 3600, 1800),
            MakeSession(processId, from.AddDays(1).AddHours(10), from.AddDays(1).AddHours(11), 3600, 1800),
            MakeSession(processId, from.AddDays(2).AddHours(10), from.AddDays(2).AddHours(11), 3600, 1800),
        };

        var result = DailyBucketing.Bucket(
            sessions,
            Array.Empty<TimeAdjustment>(),
            from,
            to,
            to,
            Utc);

        Assert.Equal(3, result.Count);
        Assert.Equal(new DateOnly(2026, 5, 7), result[0].Day);
        Assert.Equal(new DateOnly(2026, 5, 8), result[1].Day);
        Assert.Equal(new DateOnly(2026, 5, 9), result[2].Day);
        foreach (var bucket in result)
        {
            Assert.Equal(3600L, bucket.RunningSeconds);
            Assert.Equal(1800L, bucket.ForegroundSeconds);
        }
    }

    [Fact]
    public void Bucket_SessionSpanningMidnight_SplitsProportionally()
    {
        var processId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 9, 0, 0, 0, TimeSpan.Zero);
        // 2-hour session: 23:00 day1 -> 01:00 day2. Half on each day.
        var session = MakeSession(
            processId,
            new DateTimeOffset(2026, 5, 7, 23, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 8, 1, 0, 0, TimeSpan.Zero),
            7200,
            3600);

        var result = DailyBucketing.Bucket(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            from,
            to,
            to,
            Utc);

        Assert.Equal(2, result.Count);
        var day7 = result.Single(b => b.Day == new DateOnly(2026, 5, 7));
        var day8 = result.Single(b => b.Day == new DateOnly(2026, 5, 8));
        Assert.Equal(3600L, day7.RunningSeconds);
        Assert.Equal(1800L, day7.ForegroundSeconds);
        Assert.Equal(3600L, day8.RunningSeconds);
        Assert.Equal(1800L, day8.ForegroundSeconds);
    }

    [Fact]
    public void Bucket_RangeStartsMidDay_FirstBucketCoversFromOnward()
    {
        var processId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 5, 7, 18, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 8, 6, 0, 0, TimeSpan.Zero);
        // Session entirely inside day 1's portion of the range (18:00-19:00).
        var session = MakeSession(
            processId,
            new DateTimeOffset(2026, 5, 7, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 7, 19, 0, 0, TimeSpan.Zero),
            3600,
            1800);

        var result = DailyBucketing.Bucket(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            from,
            to,
            to,
            Utc);

        var bucket = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 5, 7), bucket.Day);
        Assert.Equal(3600L, bucket.RunningSeconds);
    }

    [Fact]
    public void Bucket_AdjustmentAtDayBoundary_FallsOnNextDay()
    {
        var processId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 9, 0, 0, 0, TimeSpan.Zero);
        // Adjustment at exactly midnight of day 2 -> belongs to day 2 (boundary inclusive on lower bound).
        var adjustment = MakeAdjustment(
            processId,
            TimeAdjustmentTypes.Running,
            600,
            new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero));

        var result = DailyBucketing.Bucket(
            Array.Empty<UsageSession>(),
            new[] { adjustment },
            from,
            to,
            to,
            Utc);

        var bucket = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 5, 8), bucket.Day);
        Assert.Equal(600L, bucket.RunningSeconds);
    }

    [Fact]
    public void Bucket_LocalTimeZoneShiftsDayBoundaries()
    {
        var processId = Guid.NewGuid();
        // Range: midnight UTC May 7 -> midnight UTC May 8 (local UTC+03 = 03:00 May 7 -> 03:00 May 8).
        var from = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);
        // Session at 02:00-02:30 UTC May 7 = local 05:00-05:30 May 7.
        var session = MakeSession(
            processId,
            new DateTimeOffset(2026, 5, 7, 2, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 7, 2, 30, 0, TimeSpan.Zero),
            1800,
            900);

        var result = DailyBucketing.Bucket(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            from,
            to,
            to,
            PlusThree);

        // The range in local time straddles two days (May 7 03:00 local through May 8 03:00 local).
        // The session at 05:00-05:30 local is on May 7 local.
        var bucket = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 5, 7), bucket.Day);
        Assert.Equal(1800L, bucket.RunningSeconds);
    }

    [Fact]
    public void Bucket_OpenSession_UsesNowAsUpperBound()
    {
        var processId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero);
        // Session started at 11:00, still open at "now" (12:00). Counters: 3600/1800.
        var session = new UsageSession
        {
            Id = Guid.NewGuid(),
            TrackedProcessId = processId,
            SessionStart = new DateTimeOffset(2026, 5, 7, 11, 0, 0, TimeSpan.Zero),
            SessionEnd = null,
            TotalRunningSeconds = 3600,
            ForegroundSeconds = 1800,
        };

        var result = DailyBucketing.Bucket(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            from,
            to,
            now,
            Utc);

        var bucket = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 5, 7), bucket.Day);
        Assert.Equal(3600L, bucket.RunningSeconds);
        Assert.Equal(1800L, bucket.ForegroundSeconds);
    }

    [Fact]
    public void Bucket_ZeroOnlyContributions_AreOmitted()
    {
        // Session ends before range -> no overlap -> no bucket should be produced.
        var processId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 9, 0, 0, 0, TimeSpan.Zero);
        var session = MakeSession(
            processId,
            new DateTimeOffset(2026, 5, 6, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 6, 2, 0, 0, TimeSpan.Zero),
            3600,
            1800);

        var result = DailyBucketing.Bucket(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            from,
            to,
            to,
            Utc);

        Assert.Empty(result);
    }

    private static UsageSession MakeSession(
        Guid trackedProcessId,
        DateTimeOffset start,
        DateTimeOffset end,
        long running,
        long foreground)
    {
        return new UsageSession
        {
            Id = Guid.NewGuid(),
            TrackedProcessId = trackedProcessId,
            SessionStart = start,
            SessionEnd = end,
            TotalRunningSeconds = running,
            ForegroundSeconds = foreground,
        };
    }

    private static TimeAdjustment MakeAdjustment(
        Guid trackedProcessId,
        string adjustmentType,
        long seconds,
        DateTimeOffset appliedAt)
    {
        return new TimeAdjustment
        {
            Id = Guid.NewGuid(),
            TrackedProcessId = trackedProcessId,
            AdjustmentType = adjustmentType,
            AdjustmentSeconds = seconds,
            AppliedAt = appliedAt,
        };
    }
}
