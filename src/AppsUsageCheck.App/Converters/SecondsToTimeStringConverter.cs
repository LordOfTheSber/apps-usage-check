using System.Globalization;
using System.Windows.Data;

namespace AppsUsageCheck.App.Converters;

public sealed class SecondsToTimeStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            int seconds => FormatSeconds(seconds),
            long seconds => FormatSeconds(seconds),
            _ => "0s",
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public static string FormatSeconds(long seconds)
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
