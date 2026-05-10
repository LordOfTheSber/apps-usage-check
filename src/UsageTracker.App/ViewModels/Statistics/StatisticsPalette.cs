using SkiaSharp;

namespace UsageTracker.App.ViewModels.Statistics;

public static class StatisticsPalette
{
    private static readonly SKColor[] Colors =
    {
        new(0x1B, 0x6B, 0x7A), // teal
        new(0xD7, 0x8A, 0x47), // orange
        new(0x6E, 0x9B, 0x5E), // green
        new(0xC4, 0x45, 0x45), // red
        new(0x4E, 0x6E, 0xC9), // blue
        new(0xB0, 0x6F, 0xAB), // lavender
        new(0xD4, 0xA8, 0x3F), // gold
        new(0x6F, 0x5E, 0x9B), // indigo
        new(0xD8, 0x5A, 0x8A), // rose
        new(0x9E, 0x7B, 0x4F), // brown
        new(0x3F, 0xA8, 0xB8), // cyan
        new(0x55, 0x5E, 0x68), // slate
    };

    public static int Count => Colors.Length;

    public static SKColor ForIndex(int index)
    {
        var i = ((index % Colors.Length) + Colors.Length) % Colors.Length;
        return Colors[i];
    }

    public static SKColor For(Guid trackedProcessId)
    {
        var hash = trackedProcessId.GetHashCode();
        var index = (hash & int.MaxValue) % Colors.Length;
        return Colors[index];
    }
}
