using UsageTracker.App.Models;
using UsageTracker.App.Services;
using UsageTracker.App.ViewModels;
using UsageTracker.Core.Enums;
using UsageTracker.Core.Interfaces;
using UsageTracker.Core.Models;
using UsageTracker.Infrastructure.Services;
using Xunit;

namespace UsageTracker.App.Tests;

public sealed class MainViewModelEditProcessTests
{
    [Fact]
    public async Task EditCommand_RenameOnly_UpdatesDisplayNameAndSkipsTimeAdjustment()
    {
        var trackedProcessId = Guid.NewGuid();
        var trackingEngine = new FakeTrackingEngine(
            [
                new ProcessStatus
                {
                    TrackedProcessId = trackedProcessId,
                    ProcessName = "code",
                    TrackingState = TrackingState.Active,
                }
            ]);
        var dialogService = new FakeDialogService
        {
            EditProcessDialogResult = new EditProcessResult(
                new RenameProcessRequest("Visual Studio Code"),
                TimeAdjustment: null),
        };

        using var viewModel = new MainViewModel(
            trackingEngine,
            dialogService,
            TimeProvider.System,
            new FakeDatabaseHealthCheck());

        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        var item = Assert.Single(viewModel.Processes);
        await item.EditCommand.ExecuteAsync(null);

        Assert.Equal([(trackedProcessId, "Visual Studio Code")], trackingEngine.RenameRequests);
        Assert.Empty(trackingEngine.TimeAdjustmentRequests);
        Assert.Equal(trackedProcessId, dialogService.LastEditStatus!.TrackedProcessId);
        Assert.Equal("Visual Studio Code", item.PrimaryName);
        Assert.Equal("code", item.SecondaryName);
    }

    [Fact]
    public async Task EditCommand_TimeOnly_AppliesAdjustmentAndSkipsRename()
    {
        var trackedProcessId = Guid.NewGuid();
        var trackingEngine = new FakeTrackingEngine(
            [
                new ProcessStatus
                {
                    TrackedProcessId = trackedProcessId,
                    ProcessName = "code",
                    DisplayName = "Visual Studio Code",
                    TrackingState = TrackingState.Active,
                    TotalRunningSeconds = 60,
                    ForegroundSeconds = 30,
                }
            ]);
        var dialogService = new FakeDialogService
        {
            EditProcessDialogResult = new EditProcessResult(
                Rename: null,
                new EditTimeRequest(TimeAdjustmentTarget.Running, 1800, "missed session")),
        };

        using var viewModel = new MainViewModel(
            trackingEngine,
            dialogService,
            TimeProvider.System,
            new FakeDatabaseHealthCheck());

        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        var item = Assert.Single(viewModel.Processes);
        await item.EditCommand.ExecuteAsync(null);

        Assert.Empty(trackingEngine.RenameRequests);
        Assert.Equal(
            [(trackedProcessId, TimeAdjustmentTarget.Running, 1800L, "missed session")],
            trackingEngine.TimeAdjustmentRequests);
    }

    [Fact]
    public async Task EditCommand_BothChanges_AppliesRenameThenTimeAdjustment()
    {
        var trackedProcessId = Guid.NewGuid();
        var trackingEngine = new FakeTrackingEngine(
            [
                new ProcessStatus
                {
                    TrackedProcessId = trackedProcessId,
                    ProcessName = "code",
                    TrackingState = TrackingState.Active,
                }
            ]);
        var dialogService = new FakeDialogService
        {
            EditProcessDialogResult = new EditProcessResult(
                new RenameProcessRequest("Visual Studio Code"),
                new EditTimeRequest(TimeAdjustmentTarget.Foreground, -120, null)),
        };

        using var viewModel = new MainViewModel(
            trackingEngine,
            dialogService,
            TimeProvider.System,
            new FakeDatabaseHealthCheck());

        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        var item = Assert.Single(viewModel.Processes);
        await item.EditCommand.ExecuteAsync(null);

        Assert.Equal([(trackedProcessId, "Visual Studio Code")], trackingEngine.RenameRequests);
        Assert.Equal(
            [(trackedProcessId, TimeAdjustmentTarget.Foreground, -120L, (string?)null)],
            trackingEngine.TimeAdjustmentRequests);
        Assert.Equal("Visual Studio Code", item.PrimaryName);
    }

    [Fact]
    public async Task EditCommand_DialogCancelled_NoEngineCalls()
    {
        var trackedProcessId = Guid.NewGuid();
        var trackingEngine = new FakeTrackingEngine(
            [
                new ProcessStatus
                {
                    TrackedProcessId = trackedProcessId,
                    ProcessName = "code",
                    TrackingState = TrackingState.Active,
                }
            ]);
        var dialogService = new FakeDialogService
        {
            EditProcessDialogResult = null,
        };

        using var viewModel = new MainViewModel(
            trackingEngine,
            dialogService,
            TimeProvider.System,
            new FakeDatabaseHealthCheck());

        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        var item = Assert.Single(viewModel.Processes);
        await item.EditCommand.ExecuteAsync(null);

        Assert.Empty(trackingEngine.RenameRequests);
        Assert.Empty(trackingEngine.TimeAdjustmentRequests);
    }

    private sealed class FakeTrackingEngine : ITrackingEngine
    {
        public FakeTrackingEngine(IReadOnlyList<ProcessStatus> statuses)
        {
            Statuses = statuses;
        }

        public IReadOnlyList<ProcessStatus> Statuses { get; private set; }

        public List<(Guid TrackedProcessId, string? DisplayName)> RenameRequests { get; } = [];

        public List<(Guid TrackedProcessId, TimeAdjustmentTarget Target, long AdjustmentSeconds, string? Reason)> TimeAdjustmentRequests { get; } = [];

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IReadOnlyList<ProcessStatus> GetAllStatuses() => Statuses;

        public Task<TrackedProcess> AddTrackedProcessAsync(string processName, string? displayName = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateTrackedProcessDisplayNameAsync(Guid trackedProcessId, string? displayName, CancellationToken cancellationToken = default)
        {
            RenameRequests.Add((trackedProcessId, displayName));
            Statuses = Statuses
                .Select(
                    status => status.TrackedProcessId == trackedProcessId
                        ? CloneStatus(status, displayName)
                        : CloneStatus(status, status.DisplayName))
                .ToArray();
            return Task.CompletedTask;
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
            TimeAdjustmentRequests.Add((trackedProcessId, target, adjustmentSeconds, reason));
            return Task.CompletedTask;
        }

        private static ProcessStatus CloneStatus(ProcessStatus status, string? displayName)
        {
            return new ProcessStatus
            {
                TrackedProcessId = status.TrackedProcessId,
                ProcessName = status.ProcessName,
                DisplayName = displayName,
                TrackingState = status.TrackingState,
                IsRunning = status.IsRunning,
                IsForeground = status.IsForeground,
                TotalRunningSeconds = status.TotalRunningSeconds,
                ForegroundSeconds = status.ForegroundSeconds,
                CurrentSessionRunningSeconds = status.CurrentSessionRunningSeconds,
                CurrentSessionForegroundSeconds = status.CurrentSessionForegroundSeconds,
                CurrentSessionStart = status.CurrentSessionStart,
            };
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public EditProcessResult? EditProcessDialogResult { get; set; }

        public ProcessStatus? LastEditStatus { get; private set; }

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
            LastEditStatus = status;
            return Task.FromResult(EditProcessDialogResult);
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
