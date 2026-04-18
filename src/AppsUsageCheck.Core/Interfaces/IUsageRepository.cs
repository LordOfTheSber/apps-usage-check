using AppsUsageCheck.Core.Models;

namespace AppsUsageCheck.Core.Interfaces;

public interface IUsageRepository
{
    Task<IReadOnlyList<TrackedProcess>> GetTrackedProcessesAsync(CancellationToken cancellationToken = default);

    Task<TrackedProcess?> GetTrackedProcessByNameAsync(string normalizedProcessName, CancellationToken cancellationToken = default);

    Task AddTrackedProcessAsync(TrackedProcess trackedProcess, CancellationToken cancellationToken = default);

    Task UpdateTrackedProcessAsync(TrackedProcess trackedProcess, CancellationToken cancellationToken = default);

    Task RemoveTrackedProcessAsync(Guid trackedProcessId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsageSession>> GetOpenSessionsAsync(CancellationToken cancellationToken = default);

    Task<UsageSession?> GetOpenSessionAsync(Guid trackedProcessId, CancellationToken cancellationToken = default);

    Task AddUsageSessionAsync(UsageSession session, CancellationToken cancellationToken = default);

    Task UpdateUsageSessionAsync(UsageSession session, CancellationToken cancellationToken = default);

    Task AddTimeAdjustmentAsync(TimeAdjustment adjustment, CancellationToken cancellationToken = default);

    Task<long> GetTotalRunningSecondsAsync(Guid trackedProcessId, CancellationToken cancellationToken = default);

    Task<long> GetTotalForegroundSecondsAsync(Guid trackedProcessId, CancellationToken cancellationToken = default);

    Task<long> GetTotalRunningSecondsAsync(
        Guid trackedProcessId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<long> GetTotalForegroundSecondsAsync(
        Guid trackedProcessId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
