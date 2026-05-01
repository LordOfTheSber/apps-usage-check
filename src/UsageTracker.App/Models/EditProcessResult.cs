namespace UsageTracker.App.Models;

public sealed record EditProcessResult(
    RenameProcessRequest? Rename,
    EditTimeRequest? TimeAdjustment);
