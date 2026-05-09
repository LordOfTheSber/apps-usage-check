using UsageTracker.Core.Models;
using UsageTracker.Core.Services;
using Xunit;

namespace UsageTracker.Core.Tests;

public sealed class UsageRangeAggregatorTests
{
    private static readonly DateTimeOffset RangeStart = new(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RangeEnd = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 5, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Aggregate_SessionEntirelyInsideRange_ContributesFullSeconds()
    {
        var processId = Guid.NewGuid();
        var session = MakeSession(processId, RangeStart.AddHours(2), RangeStart.AddHours(3), 3600, 1800);

        var result = UsageRangeAggregator.Aggregate(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        var contribution = Assert.Single(result);
        Assert.Equal(processId, contribution.TrackedProcessId);
        Assert.Equal(3600L, contribution.RunningSeconds);
        Assert.Equal(1800L, contribution.ForegroundSeconds);
    }

    [Fact]
    public void Aggregate_SessionEntirelyBeforeRange_ContributesNothing()
    {
        var session = MakeSession(Guid.NewGuid(), RangeStart.AddHours(-3), RangeStart.AddHours(-1), 3600, 1800);

        var result = UsageRangeAggregator.Aggregate(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_SessionEntirelyAfterRange_ContributesNothing()
    {
        var session = MakeSession(Guid.NewGuid(), RangeEnd.AddHours(1), RangeEnd.AddHours(2), 3600, 1800);

        var result = UsageRangeAggregator.Aggregate(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_SessionOverlapsRangeStart_ContributesProportionalSplit()
    {
        var processId = Guid.NewGuid();
        // 2-hour session, 1 hour before range and 1 hour inside.
        var session = MakeSession(
            processId,
            RangeStart.AddHours(-1),
            RangeStart.AddHours(1),
            running: 7200,
            foreground: 3600);

        var result = UsageRangeAggregator.Aggregate(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        var contribution = Assert.Single(result);
        Assert.Equal(3600L, contribution.RunningSeconds);
        Assert.Equal(1800L, contribution.ForegroundSeconds);
    }

    [Fact]
    public void Aggregate_SessionOverlapsRangeEnd_ContributesProportionalSplit()
    {
        var processId = Guid.NewGuid();
        // 2-hour session, 1 hour inside range and 1 hour after.
        var session = MakeSession(
            processId,
            RangeEnd.AddHours(-1),
            RangeEnd.AddHours(1),
            running: 7200,
            foreground: 3600);

        var result = UsageRangeAggregator.Aggregate(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        var contribution = Assert.Single(result);
        Assert.Equal(3600L, contribution.RunningSeconds);
        Assert.Equal(1800L, contribution.ForegroundSeconds);
    }

    [Fact]
    public void Aggregate_SessionSpansEntireRange_ContributesProportionalToRangeWidth()
    {
        var processId = Guid.NewGuid();
        // 48-hour session: 24h before, full range (24h), then ends with end of range.
        // Range is 1/2 of session duration -> 50% of accumulated counters.
        var session = MakeSession(
            processId,
            RangeStart.AddHours(-24),
            RangeEnd,
            running: 8000,
            foreground: 4000);

        var result = UsageRangeAggregator.Aggregate(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        var contribution = Assert.Single(result);
        Assert.Equal(4000L, contribution.RunningSeconds);
        Assert.Equal(2000L, contribution.ForegroundSeconds);
    }

    [Fact]
    public void Aggregate_OpenSessionInsideRange_UsesNowAsUpperBound()
    {
        var processId = Guid.NewGuid();
        // Started 1h before now (which is 12:00 inside the range), still open.
        // Wall-clock duration so far = 1h. Counters: 3600/1800. Whole interval is inside range -> full contribution.
        var session = MakeOpenSession(processId, Now.AddHours(-1), running: 3600, foreground: 1800);

        var result = UsageRangeAggregator.Aggregate(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        var contribution = Assert.Single(result);
        Assert.Equal(3600L, contribution.RunningSeconds);
        Assert.Equal(1800L, contribution.ForegroundSeconds);
    }

    [Fact]
    public void Aggregate_OpenSessionStartedBeforeRange_SplitsAtRangeStart()
    {
        var processId = Guid.NewGuid();
        // Started 12h before range, range starts 12h after that, now is 12h into the range.
        // Total wall-clock so far = 24h. Range covers the second half = 12h.
        var session = MakeOpenSession(processId, RangeStart.AddHours(-12), running: 2400, foreground: 1200);

        var result = UsageRangeAggregator.Aggregate(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        var contribution = Assert.Single(result);
        Assert.Equal(1200L, contribution.RunningSeconds);
        Assert.Equal(600L, contribution.ForegroundSeconds);
    }

    [Fact]
    public void Aggregate_ZeroDurationSession_DoesNotDivideByZero()
    {
        var session = MakeSession(Guid.NewGuid(), RangeStart.AddHours(2), RangeStart.AddHours(2), 100, 100);

        var result = UsageRangeAggregator.Aggregate(
            new[] { session },
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_MultipleSessionsForSameProcess_AreSummed()
    {
        var processId = Guid.NewGuid();
        var sessionOne = MakeSession(processId, RangeStart.AddHours(1), RangeStart.AddHours(2), 3600, 1800);
        var sessionTwo = MakeSession(processId, RangeStart.AddHours(3), RangeStart.AddHours(4), 1800, 900);

        var result = UsageRangeAggregator.Aggregate(
            new[] { sessionOne, sessionTwo },
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        var contribution = Assert.Single(result);
        Assert.Equal(5400L, contribution.RunningSeconds);
        Assert.Equal(2700L, contribution.ForegroundSeconds);
    }

    [Fact]
    public void Aggregate_MultipleProcesses_AreGroupedSeparately()
    {
        var processA = Guid.NewGuid();
        var processB = Guid.NewGuid();
        var sessionA = MakeSession(processA, RangeStart.AddHours(1), RangeStart.AddHours(2), 3600, 1800);
        var sessionB = MakeSession(processB, RangeStart.AddHours(2), RangeStart.AddHours(3), 1200, 600);

        var result = UsageRangeAggregator.Aggregate(
            new[] { sessionA, sessionB },
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        Assert.Equal(2, result.Count);
        var contributionA = result.Single(r => r.TrackedProcessId == processA);
        var contributionB = result.Single(r => r.TrackedProcessId == processB);
        Assert.Equal(3600L, contributionA.RunningSeconds);
        Assert.Equal(1800L, contributionA.ForegroundSeconds);
        Assert.Equal(1200L, contributionB.RunningSeconds);
        Assert.Equal(600L, contributionB.ForegroundSeconds);
    }

    [Fact]
    public void Aggregate_AdjustmentInsideRange_ContributesFullValue()
    {
        var processId = Guid.NewGuid();
        var adjustment = MakeAdjustment(processId, TimeAdjustmentTypes.Running, 600, RangeStart.AddHours(5));

        var result = UsageRangeAggregator.Aggregate(
            Array.Empty<UsageSession>(),
            new[] { adjustment },
            RangeStart,
            RangeEnd,
            Now);

        var contribution = Assert.Single(result);
        Assert.Equal(processId, contribution.TrackedProcessId);
        Assert.Equal(600L, contribution.RunningSeconds);
        Assert.Equal(0L, contribution.ForegroundSeconds);
    }

    [Fact]
    public void Aggregate_ForegroundAdjustment_AppliesToForegroundOnly()
    {
        var processId = Guid.NewGuid();
        var adjustment = MakeAdjustment(processId, TimeAdjustmentTypes.Foreground, 300, RangeStart.AddHours(5));

        var result = UsageRangeAggregator.Aggregate(
            Array.Empty<UsageSession>(),
            new[] { adjustment },
            RangeStart,
            RangeEnd,
            Now);

        var contribution = Assert.Single(result);
        Assert.Equal(0L, contribution.RunningSeconds);
        Assert.Equal(300L, contribution.ForegroundSeconds);
    }

    [Fact]
    public void Aggregate_AdjustmentAtRangeStart_IsIncluded()
    {
        var adjustment = MakeAdjustment(Guid.NewGuid(), TimeAdjustmentTypes.Running, 100, RangeStart);

        var result = UsageRangeAggregator.Aggregate(
            Array.Empty<UsageSession>(),
            new[] { adjustment },
            RangeStart,
            RangeEnd,
            Now);

        var contribution = Assert.Single(result);
        Assert.Equal(100L, contribution.RunningSeconds);
    }

    [Fact]
    public void Aggregate_AdjustmentAtRangeEnd_IsExcluded()
    {
        var adjustment = MakeAdjustment(Guid.NewGuid(), TimeAdjustmentTypes.Running, 100, RangeEnd);

        var result = UsageRangeAggregator.Aggregate(
            Array.Empty<UsageSession>(),
            new[] { adjustment },
            RangeStart,
            RangeEnd,
            Now);

        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_AdjustmentBeforeRange_IsExcluded()
    {
        var adjustment = MakeAdjustment(Guid.NewGuid(), TimeAdjustmentTypes.Running, 100, RangeStart.AddSeconds(-1));

        var result = UsageRangeAggregator.Aggregate(
            Array.Empty<UsageSession>(),
            new[] { adjustment },
            RangeStart,
            RangeEnd,
            Now);

        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_MixedSessionsAndAdjustments_BothContribute()
    {
        var processId = Guid.NewGuid();
        var session = MakeSession(processId, RangeStart.AddHours(1), RangeStart.AddHours(2), 3600, 1800);
        var runningAdjustment = MakeAdjustment(processId, TimeAdjustmentTypes.Running, 600, RangeStart.AddHours(5));
        var foregroundAdjustment = MakeAdjustment(processId, TimeAdjustmentTypes.Foreground, 300, RangeStart.AddHours(6));

        var result = UsageRangeAggregator.Aggregate(
            new[] { session },
            new[] { runningAdjustment, foregroundAdjustment },
            RangeStart,
            RangeEnd,
            Now);

        var contribution = Assert.Single(result);
        Assert.Equal(4200L, contribution.RunningSeconds);
        Assert.Equal(2100L, contribution.ForegroundSeconds);
    }

    [Fact]
    public void Aggregate_EmptyInputs_ReturnsEmpty()
    {
        var result = UsageRangeAggregator.Aggregate(
            Array.Empty<UsageSession>(),
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeEnd,
            Now);

        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_EndNotAfterStart_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UsageRangeAggregator.Aggregate(
            Array.Empty<UsageSession>(),
            Array.Empty<TimeAdjustment>(),
            RangeStart,
            RangeStart,
            Now));
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

    private static UsageSession MakeOpenSession(
        Guid trackedProcessId,
        DateTimeOffset start,
        long running,
        long foreground)
    {
        return new UsageSession
        {
            Id = Guid.NewGuid(),
            TrackedProcessId = trackedProcessId,
            SessionStart = start,
            SessionEnd = null,
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
