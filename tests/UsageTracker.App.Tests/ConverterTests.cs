using System.Globalization;
using System.Windows;
using System.Windows.Media;
using UsageTracker.App.Converters;
using UsageTracker.App.ViewModels;
using UsageTracker.Infrastructure.Services;
using Xunit;

namespace UsageTracker.App.Tests;

public sealed class ConverterTests
{
    [Fact]
    public void SecondsToTimeStringConverter_FormatsIntegerAndLongValues()
    {
        var converter = new SecondsToTimeStringConverter();

        Assert.Equal("1m 5s", converter.Convert(65, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("1h 1m 1s", converter.Convert(3661L, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void SecondsToTimeStringConverter_UnsupportedValue_ReturnsZeroSeconds()
    {
        var converter = new SecondsToTimeStringConverter();

        Assert.Equal("0s", converter.Convert("65", typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void SecondsToTimeStringConverter_ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new SecondsToTimeStringConverter();

        Assert.Throws<NotSupportedException>(
            () => converter.ConvertBack("1m", typeof(long), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(true, null, Visibility.Visible)]
    [InlineData(false, null, Visibility.Collapsed)]
    [InlineData(true, "invert", Visibility.Collapsed)]
    [InlineData(false, "invert", Visibility.Visible)]
    [InlineData("not-bool", null, Visibility.Collapsed)]
    public void BoolToVisibilityConverter_ConvertsExpectedVisibility(
        object? value,
        string? parameter,
        Visibility expected)
    {
        var converter = new BoolToVisibilityConverter();

        var result = converter.Convert(value, typeof(Visibility), parameter, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BoolToVisibilityConverter_ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new BoolToVisibilityConverter();

        Assert.Throws<NotSupportedException>(
            () => converter.ConvertBack(Visibility.Visible, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(ProcessItemStatusIndicatorState.Running)]
    [InlineData(ProcessItemStatusIndicatorState.Paused)]
    [InlineData(ProcessItemStatusIndicatorState.Idle)]
    public void ProcessItemStatusBrushConverter_KnownStates_ReturnFrozenBrush(ProcessItemStatusIndicatorState state)
    {
        var converter = new ProcessItemStatusBrushConverter();

        var brush = Assert.IsType<SolidColorBrush>(
            converter.Convert(state, typeof(Brush), null, CultureInfo.InvariantCulture));

        Assert.True(brush.IsFrozen);
    }

    [Fact]
    public void ProcessItemStatusBrushConverter_InvalidValue_ReturnsFrozenFallbackBrush()
    {
        var converter = new ProcessItemStatusBrushConverter();

        var brush = Assert.IsType<SolidColorBrush>(
            converter.Convert("invalid", typeof(Brush), null, CultureInfo.InvariantCulture));

        Assert.True(brush.IsFrozen);
    }

    [Theory]
    [InlineData(DatabaseConnectionState.Connected)]
    [InlineData(DatabaseConnectionState.Disconnected)]
    [InlineData(DatabaseConnectionState.Unknown)]
    public void ConnectionStatusBrushConverter_KnownStates_ReturnFrozenBrush(DatabaseConnectionState state)
    {
        var converter = new ConnectionStatusBrushConverter();

        var brush = Assert.IsType<SolidColorBrush>(
            converter.Convert(state, typeof(Brush), null, CultureInfo.InvariantCulture));

        Assert.True(brush.IsFrozen);
    }

    [Fact]
    public void ConnectionStatusBrushConverter_InvalidValue_ReturnsFrozenFallbackBrush()
    {
        var converter = new ConnectionStatusBrushConverter();

        var brush = Assert.IsType<SolidColorBrush>(
            converter.Convert("invalid", typeof(Brush), null, CultureInfo.InvariantCulture));

        Assert.True(brush.IsFrozen);
    }
}
