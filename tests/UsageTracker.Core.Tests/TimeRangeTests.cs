using UsageTracker.Core.Enums;
using UsageTracker.Core.Models;
using Xunit;

namespace UsageTracker.Core.Tests;

public sealed class TimeRangeTests
{
    [Fact]
    public void Create_AllTimePreset_ReturnsUnboundedRange()
    {
        var localNow = new DateTimeOffset(2026, 4, 14, 18, 45, 0, TimeSpan.Zero);

        var range = TimeRange.Create(TimeRangePreset.AllTime, localNow, TimeZoneInfo.Utc);

        Assert.Equal(DateTimeOffset.MinValue, range.From);
        Assert.Equal(DateTimeOffset.MaxValue, range.To);
        Assert.True(range.IsLiveAt(localNow));
    }

    [Fact]
    public void Create_TodayPreset_ReturnsLocalDayBounds()
    {
        var localNow = new DateTimeOffset(2026, 4, 14, 18, 45, 0, TimeSpan.FromHours(3));
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("Test/+03", TimeSpan.FromHours(3), "Test/+03", "Test/+03");

        var range = TimeRange.Create(TimeRangePreset.Today, localNow, timeZone);

        Assert.Equal(new DateTimeOffset(2026, 4, 14, 0, 0, 0, TimeSpan.FromHours(3)), range.From);
        Assert.Equal(new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.FromHours(3)), range.To);
        Assert.True(range.IsLiveAt(localNow));
    }

    [Fact]
    public void Create_LastWeekPreset_StartsOnPreviousMondayAndEndsOnCurrentMonday()
    {
        var localNow = new DateTimeOffset(2026, 4, 16, 9, 0, 0, TimeSpan.Zero);
        var timeZone = TimeZoneInfo.Utc;

        var range = TimeRange.Create(TimeRangePreset.LastWeek, localNow, timeZone);

        Assert.Equal(new DateTimeOffset(2026, 4, 6, 0, 0, 0, TimeSpan.Zero), range.From);
        Assert.Equal(new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero), range.To);
        Assert.False(range.IsLiveAt(localNow));
    }

    [Theory]
    [InlineData(TimeRangePreset.Yesterday, 2026, 4, 13, 2026, 4, 14)]
    [InlineData(TimeRangePreset.Last3Days, 2026, 4, 12, 2026, 4, 15)]
    [InlineData(TimeRangePreset.Last7Days, 2026, 4, 8, 2026, 4, 15)]
    [InlineData(TimeRangePreset.Last14Days, 2026, 4, 1, 2026, 4, 15)]
    [InlineData(TimeRangePreset.Last30Days, 2026, 3, 16, 2026, 4, 15)]
    [InlineData(TimeRangePreset.ThisWeek, 2026, 4, 13, 2026, 4, 20)]
    [InlineData(TimeRangePreset.ThisMonth, 2026, 4, 1, 2026, 5, 1)]
    [InlineData(TimeRangePreset.LastMonth, 2026, 3, 1, 2026, 4, 1)]
    public void Create_Preset_ReturnsExpectedBounds(
        TimeRangePreset preset,
        int expectedFromYear,
        int expectedFromMonth,
        int expectedFromDay,
        int expectedToYear,
        int expectedToMonth,
        int expectedToDay)
    {
        var localNow = new DateTimeOffset(2026, 4, 14, 9, 0, 0, TimeSpan.Zero);

        var range = TimeRange.Create(preset, localNow, TimeZoneInfo.Utc);

        Assert.Equal(new DateTimeOffset(expectedFromYear, expectedFromMonth, expectedFromDay, 0, 0, 0, TimeSpan.Zero), range.From);
        Assert.Equal(new DateTimeOffset(expectedToYear, expectedToMonth, expectedToDay, 0, 0, 0, TimeSpan.Zero), range.To);
    }

    [Fact]
    public void Create_CustomPreset_UsesInclusiveDates()
    {
        var localNow = new DateTimeOffset(2026, 4, 14, 9, 0, 0, TimeSpan.Zero);
        var timeZone = TimeZoneInfo.Utc;

        var range = TimeRange.Create(
            TimeRangePreset.Custom,
            localNow,
            timeZone,
            customStartDate: new DateTime(2026, 4, 10),
            customEndDate: new DateTime(2026, 4, 12));

        Assert.Equal(new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero), range.From);
        Assert.Equal(new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero), range.To);
    }

    [Fact]
    public void Create_CustomPreset_MissingStartDate_ThrowsInvalidOperationException()
    {
        var localNow = new DateTimeOffset(2026, 4, 14, 9, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<InvalidOperationException>(
            () => TimeRange.Create(
                TimeRangePreset.Custom,
                localNow,
                TimeZoneInfo.Utc,
                customEndDate: new DateTime(2026, 4, 12)));

        Assert.Contains("start date", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_CustomPreset_MissingEndDate_ThrowsInvalidOperationException()
    {
        var localNow = new DateTimeOffset(2026, 4, 14, 9, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<InvalidOperationException>(
            () => TimeRange.Create(
                TimeRangePreset.Custom,
                localNow,
                TimeZoneInfo.Utc,
                customStartDate: new DateTime(2026, 4, 10)));

        Assert.Contains("end date", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_CustomPreset_EndBeforeStart_ThrowsInvalidOperationException()
    {
        var localNow = new DateTimeOffset(2026, 4, 14, 9, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<InvalidOperationException>(
            () => TimeRange.Create(
                TimeRangePreset.Custom,
                localNow,
                TimeZoneInfo.Utc,
                customStartDate: new DateTime(2026, 4, 12),
                customEndDate: new DateTime(2026, 4, 10)));

        Assert.Contains("on or after", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_UnknownPreset_ThrowsArgumentOutOfRangeException()
    {
        var localNow = new DateTimeOffset(2026, 4, 14, 9, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => TimeRange.Create((TimeRangePreset)999, localNow, TimeZoneInfo.Utc));
    }
}
