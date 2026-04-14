using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AppsUsageCheck.App.ViewModels;

namespace AppsUsageCheck.App.Converters;

public sealed class ProcessItemStatusBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush RunningBrush = CreateBrush("#2D8A56");
    private static readonly SolidColorBrush PausedBrush = CreateBrush("#C98B15");
    private static readonly SolidColorBrush IdleBrush = CreateBrush("#7A808A");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ProcessItemViewModel item)
        {
            return IdleBrush;
        }

        if (item.IsPaused)
        {
            return PausedBrush;
        }

        return item.IsRunning ? RunningBrush : IdleBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static SolidColorBrush CreateBrush(string colorCode)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(colorCode)!;
        brush.Freeze();
        return brush;
    }
}
