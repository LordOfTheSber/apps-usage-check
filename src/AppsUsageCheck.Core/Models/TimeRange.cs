using AppsUsageCheck.Core.Enums;

namespace AppsUsageCheck.Core.Models;

public sealed record TimeRange(DateTimeOffset From, DateTimeOffset To)
{
    public bool IsLiveAt(DateTimeOffset instant)
    {
        return instant >= From && instant < To;
    }

    public static TimeRange Create(
        TimeRangePreset preset,
        DateTimeOffset localNow,
        TimeZoneInfo? timeZone = null,
        DateTime? customStartDate = null,
        DateTime? customEndDate = null)
    {
        var effectiveTimeZone = timeZone ?? TimeZoneInfo.Local;
        var today = localNow.Date;

        return preset switch
        {
            TimeRangePreset.AllTime => new TimeRange(DateTimeOffset.MinValue, DateTimeOffset.MaxValue),
            TimeRangePreset.Today => CreateDayRange(today, effectiveTimeZone),
            TimeRangePreset.Yesterday => CreateDayRange(today.AddDays(-1), effectiveTimeZone),
            TimeRangePreset.Last3Days => CreateDateRange(today.AddDays(-2), today.AddDays(1), effectiveTimeZone),
            TimeRangePreset.Last7Days => CreateDateRange(today.AddDays(-6), today.AddDays(1), effectiveTimeZone),
            TimeRangePreset.Last14Days => CreateDateRange(today.AddDays(-13), today.AddDays(1), effectiveTimeZone),
            TimeRangePreset.Last30Days => CreateDateRange(today.AddDays(-29), today.AddDays(1), effectiveTimeZone),
            TimeRangePreset.ThisWeek => CreateDateRange(StartOfWeek(today), StartOfWeek(today).AddDays(7), effectiveTimeZone),
            TimeRangePreset.LastWeek => CreateDateRange(StartOfWeek(today).AddDays(-7), StartOfWeek(today), effectiveTimeZone),
            TimeRangePreset.ThisMonth => CreateDateRange(StartOfMonth(today), StartOfMonth(today).AddMonths(1), effectiveTimeZone),
            TimeRangePreset.LastMonth => CreateDateRange(StartOfMonth(today).AddMonths(-1), StartOfMonth(today), effectiveTimeZone),
            TimeRangePreset.Custom => CreateCustomRange(customStartDate, customEndDate, effectiveTimeZone),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown time range preset."),
        };
    }

    private static TimeRange CreateCustomRange(DateTime? customStartDate, DateTime? customEndDate, TimeZoneInfo timeZone)
    {
        if (!customStartDate.HasValue)
        {
            throw new InvalidOperationException("A custom start date is required.");
        }

        if (!customEndDate.HasValue)
        {
            throw new InvalidOperationException("A custom end date is required.");
        }

        var startDate = customStartDate.Value.Date;
        var endDate = customEndDate.Value.Date;
        if (endDate < startDate)
        {
            throw new InvalidOperationException("The custom end date must be on or after the start date.");
        }

        return CreateDateRange(startDate, endDate.AddDays(1), timeZone);
    }

    private static TimeRange CreateDayRange(DateTime day, TimeZoneInfo timeZone)
    {
        return CreateDateRange(day, day.AddDays(1), timeZone);
    }

    private static TimeRange CreateDateRange(DateTime startDateInclusive, DateTime endDateExclusive, TimeZoneInfo timeZone)
    {
        var from = ToOffset(startDateInclusive, timeZone);
        var to = ToOffset(endDateExclusive, timeZone);

        return to <= from
            ? throw new InvalidOperationException("The time range must end after it starts.")
            : new TimeRange(from, to);
    }

    private static DateTimeOffset ToOffset(DateTime localDateTime, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified));
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        const DayOfWeek firstDayOfWeek = DayOfWeek.Monday;
        var dayOffset = ((int)date.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        return date.AddDays(-dayOffset);
    }

    private static DateTime StartOfMonth(DateTime date)
    {
        return new DateTime(date.Year, date.Month, 1);
    }
}
