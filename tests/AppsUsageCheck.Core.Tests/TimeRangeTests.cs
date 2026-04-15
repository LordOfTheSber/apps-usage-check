using AppsUsageCheck.Core.Enums;
using AppsUsageCheck.Core.Models;
using Xunit;

namespace AppsUsageCheck.Core.Tests;

public sealed class TimeRangeTests
{
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
}
