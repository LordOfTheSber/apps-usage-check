using System.ComponentModel;
using AppsUsageCheck.App.Models;
using AppsUsageCheck.App.Services;
using AppsUsageCheck.App.ViewModels;
using AppsUsageCheck.Core.Enums;
using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Core.Models;
using AppsUsageCheck.Infrastructure.Services;
using Xunit;

namespace AppsUsageCheck.App.Tests;

public sealed class MainViewModelSortTests
{
    [Fact]
    public void Constructor_DefaultsToProcessAscending()
    {
        using var viewModel = CreateViewModel();

        Assert.Equal(ProcessGridSortColumn.Process, viewModel.CurrentSortColumn);
        Assert.Equal(ListSortDirection.Ascending, viewModel.CurrentSortDirection);
    }

    [Fact]
    public void ApplySort_SameColumn_TogglesDirection()
    {
        using var viewModel = CreateViewModel();

        var direction = viewModel.ApplySort(ProcessGridSortColumn.Process);

        Assert.Equal(ListSortDirection.Descending, direction);
        Assert.Equal(ProcessGridSortColumn.Process, viewModel.CurrentSortColumn);
        Assert.Equal(ListSortDirection.Descending, viewModel.CurrentSortDirection);
    }

    [Fact]
    public async Task RefreshStatusesAsync_PreservesSelectedSortAcrossValueChangesAndMembershipChanges()
    {
        var first = CreateStatus("first", totalRunningSeconds: 30);
        var second = CreateStatus("second", totalRunningSeconds: 10);
        var third = CreateStatus("third", totalRunningSeconds: 20);
        var trackingEngine = new FakeTrackingEngine([first, second, third]);

        using var viewModel = CreateViewModel(trackingEngine);
        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        var initialDirection = viewModel.ApplySort(ProcessGridSortColumn.RunningTime);

        Assert.Equal(ListSortDirection.Ascending, initialDirection);
        Assert.Equal(["second", "third", "first"], viewModel.Processes.Select(process => process.ProcessName).ToArray());

        trackingEngine.Statuses =
        [
            CreateStatus("first", totalRunningSeconds: 5, trackedProcessId: first.TrackedProcessId),
            CreateStatus("third", totalRunningSeconds: 40, trackedProcessId: third.TrackedProcessId),
            CreateStatus("fourth", totalRunningSeconds: 15)
        ];

        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        Assert.Equal(ProcessGridSortColumn.RunningTime, viewModel.CurrentSortColumn);
        Assert.Equal(ListSortDirection.Ascending, viewModel.CurrentSortDirection);
        Assert.Equal(["first", "fourth", "third"], viewModel.Processes.Select(process => process.ProcessName).ToArray());
    }

    private static MainViewModel CreateViewModel(FakeTrackingEngine? trackingEngine = null)
    {
        return new MainViewModel(
            trackingEngine ?? new FakeTrackingEngine([]),
            new FakeDialogService(),
            TimeProvider.System,
            new FakeDatabaseHealthCheck());
    }

    private static ProcessStatus CreateStatus(
        string processName,
        long totalRunningSeconds,
        Guid? trackedProcessId = null)
    {
        return new ProcessStatus
        {
            TrackedProcessId = trackedProcessId ?? Guid.NewGuid(),
            ProcessName = processName,
            TrackingState = TrackingState.Active,
            TotalRunningSeconds = totalRunningSeconds,
        };
    }

    private sealed class FakeTrackingEngine : ITrackingEngine
    {
        public FakeTrackingEngine(IReadOnlyList<ProcessStatus> statuses)
        {
            Statuses = statuses;
        }

        public IReadOnlyList<ProcessStatus> Statuses { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IReadOnlyList<ProcessStatus> GetAllStatuses() => Statuses;

        public Task<TrackedProcess> AddTrackedProcessAsync(string processName, string? displayName = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateTrackedProcessDisplayNameAsync(Guid trackedProcessId, string? displayName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task RemoveTrackedProcessAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task PauseTrackingAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task ResumeTrackingAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task PauseAllTrackingAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task ResumeAllTrackingAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<Guid, UsageTotals>> GetFilteredTotalsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<Guid, UsageTotals> totals = new Dictionary<Guid, UsageTotals>();
            return Task.FromResult(totals);
        }

        public Task ApplyTimeAdjustmentAsync(
            Guid trackedProcessId,
            TimeAdjustmentTarget target,
            long adjustmentSeconds,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public Task<AddProcessRequest?> ShowAddProcessDialogAsync(
            IReadOnlyCollection<string> trackedProcessNames,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<EditProcessResult?> ShowEditProcessDialogAsync(
            ProcessStatus status,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void ShowSettingsDialog()
        {
            throw new NotSupportedException();
        }

        public bool Confirm(string title, string message)
        {
            throw new NotSupportedException();
        }

        public void ShowInformation(string title, string message)
        {
            throw new NotSupportedException();
        }

        public void ShowError(string title, string message)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeDatabaseHealthCheck : IDatabaseHealthCheck
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public DatabaseConnectionState ConnectionState => DatabaseConnectionState.Connected;

        public bool IsConnected => true;

        public string StatusText => "Connected";

        public DateTimeOffset? LastCheckedAtUtc => null;
    }
}
