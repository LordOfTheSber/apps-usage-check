using UsageTracker.Core.Enums;

namespace UsageTracker.Core.Models;

public static class TimeAdjustmentTypes
{
    public const string Running = "running";
    public const string Foreground = "foreground";

    public static string ToStorageValue(TimeAdjustmentTarget target)
    {
        return target switch
        {
            TimeAdjustmentTarget.Running => Running,
            TimeAdjustmentTarget.Foreground => Foreground,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported adjustment target."),
        };
    }
}
