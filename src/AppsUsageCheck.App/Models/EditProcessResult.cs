namespace AppsUsageCheck.App.Models;

public sealed record EditProcessResult(
    RenameProcessRequest? Rename,
    EditTimeRequest? TimeAdjustment);
