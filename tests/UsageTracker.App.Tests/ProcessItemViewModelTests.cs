using UsageTracker.App.ViewModels;
using UsageTracker.Core.Enums;
using UsageTracker.Core.Models;
using Xunit;

namespace UsageTracker.App.Tests;

public sealed class ProcessItemViewModelTests
{
    [Fact]
    public void Update_ActiveForegroundProcess_SetsPresentationProperties()
    {
        var item = CreateItem();

        item.Update(
            new ProcessStatus
            {
                TrackedProcessId = item.TrackedProcessId,
                ProcessName = "code",
                DisplayName = "Visual Studio Code",
                TrackingState = TrackingState.Active,
                IsRunning = true,
                IsForeground = true,
                TotalRunningSeconds = 120,
                ForegroundSeconds = 60,
                CurrentSessionRunningSeconds = 20,
                CurrentSessionForegroundSeconds = 10,
            });

        Assert.Equal("Visual Studio Code", item.PrimaryName);
        Assert.Equal("code", item.SecondaryName);
        Assert.False(item.IsPaused);
        Assert.Equal("Foreground", item.StateText);
        Assert.Equal("Pause", item.ActionText);
        Assert.Equal(ProcessItemStatusIndicatorState.Running, item.StatusIndicatorState);
        Assert.Equal(120, item.TotalRunningSeconds);
        Assert.Equal(60, item.ForegroundSeconds);
        Assert.Equal(20, item.CurrentSessionRunningSeconds);
        Assert.Equal(10, item.CurrentSessionForegroundSeconds);
    }

    [Fact]
    public void Update_PausedProcess_SetsPausedPresentation()
    {
        var item = CreateItem();

        item.Update(
            new ProcessStatus
            {
                TrackedProcessId = item.TrackedProcessId,
                ProcessName = "code",
                TrackingState = TrackingState.Paused,
            });

        Assert.Equal("code", item.PrimaryName);
        Assert.Equal("No display name", item.SecondaryName);
        Assert.True(item.IsPaused);
        Assert.Equal("Paused", item.StateText);
        Assert.Equal("Resume", item.ActionText);
        Assert.Equal(ProcessItemStatusIndicatorState.Paused, item.StatusIndicatorState);
    }

    [Theory]
    [InlineData(false, false, "Waiting", ProcessItemStatusIndicatorState.Idle)]
    [InlineData(true, false, "Running", ProcessItemStatusIndicatorState.Running)]
    public void Update_ProcessState_SetsStateTextAndIndicator(
        bool isRunning,
        bool isForeground,
        string expectedStateText,
        ProcessItemStatusIndicatorState expectedIndicator)
    {
        var item = CreateItem();

        item.Update(
            new ProcessStatus
            {
                TrackedProcessId = item.TrackedProcessId,
                ProcessName = "code",
                TrackingState = TrackingState.Active,
                IsRunning = isRunning,
                IsForeground = isForeground,
            });

        Assert.Equal(expectedStateText, item.StateText);
        Assert.Equal(expectedIndicator, item.StatusIndicatorState);
    }

    [Fact]
    public void SetFilteredTotals_OverridesDisplayedTotalsUntilCleared()
    {
        var item = CreateItem();
        item.Update(
            new ProcessStatus
            {
                TrackedProcessId = item.TrackedProcessId,
                ProcessName = "code",
                TrackingState = TrackingState.Active,
                TotalRunningSeconds = 100,
                ForegroundSeconds = 40,
            });

        item.SetFilteredTotals(new UsageTotals(10, 5));

        Assert.Equal(10, item.DisplayedRunningSeconds);
        Assert.Equal(5, item.DisplayedForegroundSeconds);

        item.ClearFilteredTotals();

        Assert.Equal(100, item.DisplayedRunningSeconds);
        Assert.Equal(40, item.DisplayedForegroundSeconds);
    }

    [Fact]
    public async Task TogglePauseCommand_ActiveProcess_CallsPause()
    {
        var calls = new List<Guid>();
        var item = CreateItem(pauseAsync: id =>
        {
            calls.Add(id);
            return Task.CompletedTask;
        });
        item.Update(
            new ProcessStatus
            {
                TrackedProcessId = item.TrackedProcessId,
                ProcessName = "code",
                TrackingState = TrackingState.Active,
            });

        await item.TogglePauseCommand.ExecuteAsync(null);

        Assert.Equal([item.TrackedProcessId], calls);
    }

    [Fact]
    public async Task TogglePauseCommand_PausedProcess_CallsResume()
    {
        var calls = new List<Guid>();
        var item = CreateItem(resumeAsync: id =>
        {
            calls.Add(id);
            return Task.CompletedTask;
        });
        item.Update(
            new ProcessStatus
            {
                TrackedProcessId = item.TrackedProcessId,
                ProcessName = "code",
                TrackingState = TrackingState.Paused,
            });

        await item.TogglePauseCommand.ExecuteAsync(null);

        Assert.Equal([item.TrackedProcessId], calls);
    }

    [Fact]
    public async Task Commands_CallSuppliedDelegates()
    {
        var editCalls = new List<Guid>();
        var removeCalls = new List<Guid>();
        var refreshCalls = new List<Guid>();
        var item = CreateItem(
            editAsync: id =>
            {
                editCalls.Add(id);
                return Task.CompletedTask;
            },
            removeAsync: id =>
            {
                removeCalls.Add(id);
                return Task.CompletedTask;
            },
            refreshIconAsync: id =>
            {
                refreshCalls.Add(id);
                return Task.CompletedTask;
            });

        await item.EditCommand.ExecuteAsync(null);
        await item.RemoveCommand.ExecuteAsync(null);
        await item.RefreshIconCommand.ExecuteAsync(null);

        Assert.Equal([item.TrackedProcessId], editCalls);
        Assert.Equal([item.TrackedProcessId], removeCalls);
        Assert.Equal([item.TrackedProcessId], refreshCalls);
    }

    [Fact]
    public void RefreshIconCommand_NoRefreshDelegate_IsDisabled()
    {
        var item = CreateItem(refreshIconAsync: null);

        Assert.False(item.RefreshIconCommand.CanExecute(null));
    }

    private static ProcessItemViewModel CreateItem(
        Func<Guid, Task>? pauseAsync = null,
        Func<Guid, Task>? resumeAsync = null,
        Func<Guid, Task>? editAsync = null,
        Func<Guid, Task>? removeAsync = null,
        Func<Guid, Task>? refreshIconAsync = null)
    {
        return new ProcessItemViewModel(
            Guid.NewGuid(),
            pauseAsync ?? (_ => Task.CompletedTask),
            resumeAsync ?? (_ => Task.CompletedTask),
            editAsync ?? (_ => Task.CompletedTask),
            removeAsync ?? (_ => Task.CompletedTask),
            refreshIconAsync);
    }
}
