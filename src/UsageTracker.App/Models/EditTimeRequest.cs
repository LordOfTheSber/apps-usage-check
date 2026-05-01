using UsageTracker.Core.Enums;

namespace UsageTracker.App.Models;

public sealed record EditTimeRequest(
    TimeAdjustmentTarget Target,
    long AdjustmentSeconds,
    string? Reason);
