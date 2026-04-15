using AppsUsageCheck.Core.Enums;
using AppsUsageCheck.Core.Models;

namespace AppsUsageCheck.Core.Interfaces;

public interface ITrackingEngine
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<ProcessStatus> GetAllStatuses();

    Task<TrackedProcess> AddTrackedProcessAsync(string processName, string? displayName = null, CancellationToken cancellationToken = default);

    Task RemoveTrackedProcessAsync(Guid trackedProcessId, CancellationToken cancellationToken = default);

    Task PauseTrackingAsync(Guid trackedProcessId, CancellationToken cancellationToken = default);

    Task ResumeTrackingAsync(Guid trackedProcessId, CancellationToken cancellationToken = default);

    Task PauseAllTrackingAsync(CancellationToken cancellationToken = default);

    Task ResumeAllTrackingAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, UsageTotals>> GetFilteredTotalsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task ApplyTimeAdjustmentAsync(
        Guid trackedProcessId,
        TimeAdjustmentTarget target,
        long adjustmentSeconds,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
