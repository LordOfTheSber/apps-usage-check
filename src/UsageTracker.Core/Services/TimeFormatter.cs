namespace UsageTracker.Core.Services;

public static class TimeFormatter
{
    public static string FormatDuration(long seconds)
    {
        if (seconds <= 0)
        {
            return "0s";
        }

        var total = TimeSpan.FromSeconds(seconds);
        var parts = new List<string>(3);

        if (total.Days > 0)
        {
            parts.Add($"{total.Days}d");
        }

        if (total.Hours > 0)
        {
            parts.Add($"{total.Hours}h");
        }

        if (total.Minutes > 0)
        {
            parts.Add($"{total.Minutes}m");
        }

        if (total.Seconds > 0 || parts.Count == 0)
        {
            parts.Add($"{total.Seconds}s");
        }

        return string.Join(" ", parts);
    }
}
