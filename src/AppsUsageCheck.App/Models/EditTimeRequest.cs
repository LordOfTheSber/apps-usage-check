using AppsUsageCheck.Core.Enums;

namespace AppsUsageCheck.App.Models;

public sealed record EditTimeRequest(
    TimeAdjustmentTarget Target,
    long AdjustmentSeconds,
    string? Reason);
