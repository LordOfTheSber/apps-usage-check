using System.ComponentModel;
using AppsUsageCheck.App.ViewModels;
using AppsUsageCheck.Core.Enums;
using AppsUsageCheck.Core.Models;
using Xunit;

namespace AppsUsageCheck.App.Tests;

public sealed class ProcessGridSorterTests
{
    [Fact]
    public void OrderItems_ProcessSort_UsesVisibleNameAndFallsBackToProcessName()
    {
        var alphaBeta = CreateItem(processName: "beta", displayName: "Alpha");
        var alphaZeta = CreateItem(processName: "zeta", displayName: "Alpha");
        var gamma = CreateItem(processName: "gamma", displayName: null);

        var ordered = ProcessGridSorter.OrderItems(
            [alphaZeta, gamma, alphaBeta],
            ProcessGridSortColumn.Process,
            ListSortDirection.Ascending);

        Assert.Equal([alphaBeta, alphaZeta, gamma], ordered);
    }

    [Fact]
    public void OrderItems_StateSort_UsesSemanticActivityRanking()
    {
        var foreground = CreateItem(processName: "foreground", isRunning: true, isForeground: true);
        var running = CreateItem(processName: "running", isRunning: true);
        var waiting = CreateItem(processName: "waiting");
        var paused = CreateItem(processName: "paused", trackingState: TrackingState.Paused);

        var ordered = ProcessGridSorter.OrderItems(
            [paused, waiting, running, foreground],
            ProcessGridSortColumn.State,
            ListSortDirection.Ascending);

        Assert.Equal([foreground, running, waiting, paused], ordered);
    }

    [Fact]
    public void OrderItems_RunningTimeSort_UsesDisplayedRunningSeconds()
    {
        var filteredLow = CreateItem(processName: "filtered-low", totalRunningSeconds: 100);
        filteredLow.SetFilteredTotals(new UsageTotals(10, 0));

        var unfilteredHigh = CreateItem(processName: "unfiltered-high", totalRunningSeconds: 50);

        var ordered = ProcessGridSorter.OrderItems(
            [unfilteredHigh, filteredLow],
            ProcessGridSortColumn.RunningTime,
            ListSortDirection.Ascending);

        Assert.Equal([filteredLow, unfilteredHigh], ordered);
    }

    [Fact]
    public void OrderItems_ForegroundTimeSort_UsesDisplayedForegroundSeconds()
    {
        var filteredLow = CreateItem(processName: "filtered-low", foregroundSeconds: 120);
        filteredLow.SetFilteredTotals(new UsageTotals(0, 5));

        var unfilteredHigh = CreateItem(processName: "unfiltered-high", foregroundSeconds: 30);

        var ordered = ProcessGridSorter.OrderItems(
            [unfilteredHigh, filteredLow],
            ProcessGridSortColumn.ForegroundTime,
            ListSortDirection.Ascending);

        Assert.Equal([filteredLow, unfilteredHigh], ordered);
    }

    private static ProcessItemViewModel CreateItem(
        string processName,
        string? displayName = null,
        TrackingState trackingState = TrackingState.Active,
        bool isRunning = false,
        bool isForeground = false,
        long totalRunningSeconds = 0,
        long foregroundSeconds = 0)
    {
        var item = new ProcessItemViewModel(
            Guid.NewGuid(),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);

        item.Update(
            new ProcessStatus
            {
                TrackedProcessId = item.TrackedProcessId,
                ProcessName = processName,
                DisplayName = displayName,
                TrackingState = trackingState,
                IsRunning = isRunning,
                IsForeground = isForeground,
                TotalRunningSeconds = totalRunningSeconds,
                ForegroundSeconds = foregroundSeconds,
            });

        return item;
    }
}
