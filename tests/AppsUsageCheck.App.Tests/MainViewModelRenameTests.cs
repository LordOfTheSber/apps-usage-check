using AppsUsageCheck.App.Models;
using AppsUsageCheck.App.Services;
using AppsUsageCheck.App.ViewModels;
using AppsUsageCheck.Core.Enums;
using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Core.Models;
using AppsUsageCheck.Infrastructure.Services;
using Xunit;

namespace AppsUsageCheck.App.Tests;

public sealed class MainViewModelRenameTests
{
    [Fact]
    public async Task RenameCommand_RenamesDisplayNameAndRefreshesVisibleItem()
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
            RenameProcessDialogResult = new RenameProcessRequest("Visual Studio Code"),
        };

        using var viewModel = new MainViewModel(
            trackingEngine,
            dialogService,
            TimeProvider.System,
            new FakeDatabaseHealthCheck());

        await viewModel.RefreshStatusesAsync(forceFilteredTotalsRefresh: true);

        var item = Assert.Single(viewModel.Processes);
        await item.RenameCommand.ExecuteAsync(null);

        Assert.Equal([(trackedProcessId, "Visual Studio Code")], trackingEngine.RenameRequests);
        Assert.NotNull(dialogService.LastRenameStatus);
        Assert.Equal(trackedProcessId, dialogService.LastRenameStatus!.TrackedProcessId);
        Assert.Equal("Visual Studio Code", item.PrimaryName);
        Assert.Equal("code", item.SecondaryName);
    }

    private sealed class FakeTrackingEngine : ITrackingEngine
    {
        public FakeTrackingEngine(IReadOnlyList<ProcessStatus> statuses)
        {
            Statuses = statuses;
        }

        public IReadOnlyList<ProcessStatus> Statuses { get; private set; }

        public List<(Guid TrackedProcessId, string? DisplayName)> RenameRequests { get; } = [];

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
            throw new NotSupportedException();
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
        public RenameProcessRequest? RenameProcessDialogResult { get; set; }

        public ProcessStatus? LastRenameStatus { get; private set; }

        public Task<AddProcessRequest?> ShowAddProcessDialogAsync(
            IReadOnlyCollection<string> trackedProcessNames,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RenameProcessRequest?> ShowRenameProcessDialogAsync(
            ProcessStatus status,
            CancellationToken cancellationToken = default)
        {
            LastRenameStatus = status;
            return Task.FromResult(RenameProcessDialogResult);
        }

        public Task<EditTimeRequest?> ShowEditTimeDialogAsync(
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
