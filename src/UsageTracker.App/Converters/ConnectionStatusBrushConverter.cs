using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using UsageTracker.Infrastructure.Services;

namespace UsageTracker.App.Converters;

public sealed class ConnectionStatusBrushConverter : IValueConverter
{
    private static readonly Brush UnknownBrush = CreateFrozenBrush("#C29A3D");
    private static readonly Brush ConnectedBrush = CreateFrozenBrush("#2F855A");
    private static readonly Brush DisconnectedBrush = CreateFrozenBrush("#C05621");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DatabaseConnectionState state
            ? state switch
            {
                DatabaseConnectionState.Connected => ConnectedBrush,
                DatabaseConnectionState.Disconnected => DisconnectedBrush,
                _ => UnknownBrush,
            }
            : UnknownBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static Brush CreateFrozenBrush(string colorHex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex)!;
        brush.Freeze();
        return brush;
    }
}
