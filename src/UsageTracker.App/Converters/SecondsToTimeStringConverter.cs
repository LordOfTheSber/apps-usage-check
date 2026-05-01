using System.Globalization;
using System.Windows.Data;
using UsageTracker.Core.Services;

namespace UsageTracker.App.Converters;

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
        return TimeFormatter.FormatDuration(seconds);
    }
}
