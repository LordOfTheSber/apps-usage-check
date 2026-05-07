using UsageTracker.App.Models;
using UsageTracker.App.Services;
using UsageTracker.App.ViewModels;
using UsageTracker.Core.Enums;
using UsageTracker.Core.Interfaces;
using UsageTracker.Core.Models;
using UsageTracker.Infrastructure.Services;
using Xunit;

namespace UsageTracker.App.Tests;

public sealed class MainViewModelCommandTests
{
    [Fact]
    public async Task AddProcessCommand_AddsProcessAndRefreshesStatuses()
    {
        var existingStatus = CreateStatus("chrome");
        var trackingEngine = new FakeTrackingEngine([existingStatus]);
        var dialogService = new FakeDialogService
        {
            AddProcessDialogResult = new AddProcessRequest("code", "Visual Studio Code"),
        };

        using var viewModel = CreateViewModel(trackingEngine, dialogService);
        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        await viewModel.AddProcessCommand.ExecuteAsync(null);

        Assert.Equal([existingStatus.ProcessName], dialogService.LastTrackedProcessNames);
        Assert.Equal([("code", "Visual Studio Code")], trackingEngine.AddRequests);
        Assert.Equal(["chrome", "code"], viewModel.Processes.Select(process => process.ProcessName).ToArray());
    }

    [Fact]
    public async Task AddProcessCommand_DialogCancelled_DoesNotCallEngine()
    {
        var trackingEngine = new FakeTrackingEngine([]);
        var dialogService = new FakeDialogService
        {
            AddProcessDialogResult = null,
        };

        using var viewModel = CreateViewModel(trackingEngine, dialogService);

        await viewModel.AddProcessCommand.ExecuteAsync(null);

        Assert.Empty(trackingEngine.AddRequests);
    }

    [Fact]
    public async Task PauseAllCommand_PausesActiveProcessesAndRefreshes()
    {
        var activeId = Guid.NewGuid();
        var trackingEngine = new FakeTrackingEngine(
            [CreateStatus("code", activeId, TrackingState.Active)]);

        using var viewModel = CreateViewModel(trackingEngine, new FakeDialogService());
        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        await viewModel.PauseAllCommand.ExecuteAsync(null);

        Assert.Equal(1, trackingEngine.PauseAllCallCount);
        var item = Assert.Single(viewModel.Processes);
        Assert.True(item.IsPaused);
    }

    [Fact]
    public async Task ResumeAllCommand_ResumesPausedProcessesAndRefreshes()
    {
        var pausedId = Guid.NewGuid();
        var trackingEngine = new FakeTrackingEngine(
            [CreateStatus("code", pausedId, TrackingState.Paused)]);

        using var viewModel = CreateViewModel(trackingEngine, new FakeDialogService());
        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        await viewModel.ResumeAllCommand.ExecuteAsync(null);

        Assert.Equal(1, trackingEngine.ResumeAllCallCount);
        var item = Assert.Single(viewModel.Processes);
        Assert.False(item.IsPaused);
    }

    [Fact]
    public async Task RemoveCommand_WhenConfirmationCancelled_DoesNotRemove()
    {
        var trackedProcessId = Guid.NewGuid();
        var trackingEngine = new FakeTrackingEngine(
            [CreateStatus("code", trackedProcessId, TrackingState.Active)]);
        var dialogService = new FakeDialogService
        {
            ConfirmResult = false,
        };

        using var viewModel = CreateViewModel(trackingEngine, dialogService);
        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        await Assert.Single(viewModel.Processes).RemoveCommand.ExecuteAsync(null);

        Assert.Empty(trackingEngine.RemoveRequests);
        Assert.Single(viewModel.Processes);
    }

    [Fact]
    public async Task RemoveCommand_WhenConfirmed_RemovesProcessAndRefreshes()
    {
        var trackedProcessId = Guid.NewGuid();
        var trackingEngine = new FakeTrackingEngine(
            [CreateStatus("code", trackedProcessId, TrackingState.Active)]);
        var dialogService = new FakeDialogService
        {
            ConfirmResult = true,
        };

        using var viewModel = CreateViewModel(trackingEngine, dialogService);
        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        await Assert.Single(viewModel.Processes).RemoveCommand.ExecuteAsync(null);

        Assert.Equal([trackedProcessId], trackingEngine.RemoveRequests);
        Assert.Empty(viewModel.Processes);
    }

    [Fact]
    public async Task RefreshStatusesAsync_WhenEngineThrows_UpdatesStatusMessage()
    {
        var trackingEngine = new FakeTrackingEngine([])
        {
            GetStatusesException = new InvalidOperationException("database unavailable"),
        };

        using var viewModel = CreateViewModel(trackingEngine, new FakeDialogService());

        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        Assert.Equal("Unable to refresh usage data: database unavailable", viewModel.StatusMessage);
    }

    [Fact]
    public void CustomDateChanges_NormalizeRangeOrder()
    {
        using var viewModel = CreateViewModel(new FakeTrackingEngine([]), new FakeDialogService());

        viewModel.SelectedPreset = TimeRangePreset.Custom;
        viewModel.CustomStartDate = new DateTime(2026, 5, 10);
        viewModel.CustomEndDate = new DateTime(2026, 5, 5);

        Assert.Equal(new DateTime(2026, 5, 5), viewModel.CustomStartDate);
        Assert.Equal(new DateTime(2026, 5, 5), viewModel.CustomEndDate);

        viewModel.CustomStartDate = new DateTime(2026, 5, 12);

        Assert.Equal(new DateTime(2026, 5, 12), viewModel.CustomStartDate);
        Assert.Equal(new DateTime(2026, 5, 12), viewModel.CustomEndDate);
    }

    private static MainViewModel CreateViewModel(FakeTrackingEngine trackingEngine, FakeDialogService dialogService)
    {
        return new MainViewModel(
            trackingEngine,
            dialogService,
            TimeProvider.System,
            new FakeDatabaseHealthCheck());
    }

    private static ProcessStatus CreateStatus(
        string processName,
        Guid? trackedProcessId = null,
        TrackingState trackingState = TrackingState.Active)
    {
        return new ProcessStatus
        {
            TrackedProcessId = trackedProcessId ?? Guid.NewGuid(),
            ProcessName = processName,
            TrackingState = trackingState,
        };
    }

    private sealed class FakeTrackingEngine : ITrackingEngine
    {
        public FakeTrackingEngine(IReadOnlyList<ProcessStatus> statuses)
        {
            Statuses = statuses;
        }

        public IReadOnlyList<ProcessStatus> Statuses { get; private set; }

        public List<(string ProcessName, string? DisplayName)> AddRequests { get; } = [];

        public List<Guid> RemoveRequests { get; } = [];

        public int PauseAllCallCount { get; private set; }

        public int ResumeAllCallCount { get; private set; }

        public Exception? GetStatusesException { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IReadOnlyList<ProcessStatus> GetAllStatuses()
        {
            if (GetStatusesException is not null)
            {
                throw GetStatusesException;
            }

            return Statuses;
        }

        public Task<TrackedProcess> AddTrackedProcessAsync(string processName, string? displayName = null, CancellationToken cancellationToken = default)
        {
            AddRequests.Add((processName, displayName));

            var trackedProcess = new TrackedProcess
            {
                Id = Guid.NewGuid(),
                ProcessName = processName,
                DisplayName = displayName,
            };
            Statuses = Statuses
                .Concat(
                    [
                        new ProcessStatus
                        {
                            TrackedProcessId = trackedProcess.Id,
                            ProcessName = processName,
                            DisplayName = displayName,
                            TrackingState = TrackingState.Active,
                        }
                    ])
                .ToArray();

            return Task.FromResult(trackedProcess);
        }

        public Task UpdateTrackedProcessDisplayNameAsync(Guid trackedProcessId, string? displayName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task RemoveTrackedProcessAsync(Guid trackedProcessId, CancellationToken cancellationToken = default)
        {
            RemoveRequests.Add(trackedProcessId);
            Statuses = Statuses
                .Where(status => status.TrackedProcessId != trackedProcessId)
                .ToArray();
            return Task.CompletedTask;
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
            PauseAllCallCount++;
            Statuses = Statuses
                .Select(status => CloneStatus(status, TrackingState.Paused))
                .ToArray();
            return Task.CompletedTask;
        }

        public Task ResumeAllTrackingAsync(CancellationToken cancellationToken = default)
        {
            ResumeAllCallCount++;
            Statuses = Statuses
                .Select(status => CloneStatus(status, TrackingState.Active))
                .ToArray();
            return Task.CompletedTask;
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

        private static ProcessStatus CloneStatus(ProcessStatus status, TrackingState trackingState)
        {
            return new ProcessStatus
            {
                TrackedProcessId = status.TrackedProcessId,
                ProcessName = status.ProcessName,
                DisplayName = status.DisplayName,
                TrackingState = trackingState,
                IsRunning = trackingState == TrackingState.Active && status.IsRunning,
                IsForeground = trackingState == TrackingState.Active && status.IsForeground,
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
        public AddProcessRequest? AddProcessDialogResult { get; set; }

        public IReadOnlyCollection<string>? LastTrackedProcessNames { get; private set; }

        public bool ConfirmResult { get; set; } = true;

        public Task<AddProcessRequest?> ShowAddProcessDialogAsync(
            IReadOnlyCollection<string> trackedProcessNames,
            CancellationToken cancellationToken = default)
        {
            LastTrackedProcessNames = trackedProcessNames.ToArray();
            return Task.FromResult(AddProcessDialogResult);
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

        public bool Confirm(string title, string message) => ConfirmResult;

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
