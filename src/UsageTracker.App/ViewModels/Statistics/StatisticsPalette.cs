using SkiaSharp;

namespace UsageTracker.App.ViewModels.Statistics;

public static class StatisticsPalette
{
    private static readonly SKColor[] Colors =
    {
        new(0x1B, 0x6B, 0x7A),
        new(0xC5, 0x80, 0x4D),
        new(0x6E, 0x9B, 0x5E),
        new(0xAA, 0x3A, 0x2B),
        new(0x4E, 0x6E, 0xC9),
        new(0xB0, 0x6F, 0xAB),
        new(0x8A, 0x96, 0x53),
        new(0xD4, 0xA8, 0x3F),
        new(0x4C, 0x88, 0x80),
        new(0xCB, 0x6A, 0x68),
        new(0x6F, 0x5E, 0x9B),
        new(0x9E, 0x7B, 0x4F),
    };

    public static SKColor For(Guid trackedProcessId)
    {
        var hash = trackedProcessId.GetHashCode();
        var index = (hash & int.MaxValue) % Colors.Length;
        return Colors[index];
    }
}
