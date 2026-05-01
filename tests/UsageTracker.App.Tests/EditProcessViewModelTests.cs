using UsageTracker.App.ViewModels;
using UsageTracker.Core.Enums;
using UsageTracker.Core.Models;
using Xunit;

namespace UsageTracker.App.Tests;

public sealed class EditProcessViewModelTests
{
    [Fact]
    public void CanSubmit_FalseWhenNoChanges()
    {
        var viewModel = new EditProcessViewModel(CreateStatus());

        Assert.False(viewModel.CanSubmit);
    }

    [Fact]
    public void CanSubmit_TrueWhenOnlyRenameChanged()
    {
        var viewModel = new EditProcessViewModel(CreateStatus());

        viewModel.RenameSection.DisplayName = "New name";

        Assert.True(viewModel.CanSubmit);
    }

    [Fact]
    public void CanSubmit_TrueWhenOnlyTimeChanged()
    {
        var viewModel = new EditProcessViewModel(CreateStatus(totalRunningSeconds: 600, foregroundSeconds: 0));

        viewModel.TimeSection.MinutesText = "5";

        Assert.True(viewModel.CanSubmit);
    }

    [Fact]
    public void CanSubmit_FalseWhenTimeIsInvalidEvenIfRenameChanged()
    {
        var viewModel = new EditProcessViewModel(CreateStatus(totalRunningSeconds: 60, foregroundSeconds: 0));

        viewModel.RenameSection.DisplayName = "New name";
        viewModel.TimeSection.MinutesText = "10";
        viewModel.TimeSection.SelectedOperationOption = viewModel.TimeSection.OperationOptions.Single(option => option.IsSubtract);

        Assert.False(viewModel.CanSubmit);
    }

    [Fact]
    public void TryCreateResult_RenameOnly()
    {
        var viewModel = new EditProcessViewModel(CreateStatus())
        {
            RenameSection = { DisplayName = "Visual Studio Code" },
        };

        var created = viewModel.TryCreateResult(out var result, out var error);

        Assert.True(created);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.NotNull(result!.Rename);
        Assert.Equal("Visual Studio Code", result.Rename!.DisplayName);
        Assert.Null(result.TimeAdjustment);
    }

    [Fact]
    public void TryCreateResult_TimeOnly()
    {
        var viewModel = new EditProcessViewModel(CreateStatus(totalRunningSeconds: 0, foregroundSeconds: 0));

        viewModel.TimeSection.MinutesText = "10";

        var created = viewModel.TryCreateResult(out var result, out var error);

        Assert.True(created);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Null(result!.Rename);
        Assert.NotNull(result.TimeAdjustment);
        Assert.Equal(TimeAdjustmentTarget.Running, result.TimeAdjustment!.Target);
        Assert.Equal(600L, result.TimeAdjustment.AdjustmentSeconds);
    }

    [Fact]
    public void TryCreateResult_BothChanges()
    {
        var viewModel = new EditProcessViewModel(CreateStatus())
        {
            RenameSection = { DisplayName = "Visual Studio Code" },
        };
        viewModel.TimeSection.MinutesText = "30";

        var created = viewModel.TryCreateResult(out var result, out var error);

        Assert.True(created);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.NotNull(result!.Rename);
        Assert.NotNull(result.TimeAdjustment);
        Assert.Equal("Visual Studio Code", result.Rename!.DisplayName);
        Assert.Equal(1800L, result.TimeAdjustment!.AdjustmentSeconds);
    }

    [Fact]
    public void TryCreateResult_NoChanges_ReturnsError()
    {
        var viewModel = new EditProcessViewModel(CreateStatus());

        var created = viewModel.TryCreateResult(out var result, out var error);

        Assert.False(created);
        Assert.Null(result);
        Assert.Equal("Make a change before saving.", error);
    }

    private static ProcessStatus CreateStatus(
        long totalRunningSeconds = 0,
        long foregroundSeconds = 0)
    {
        return new ProcessStatus
        {
            TrackedProcessId = Guid.NewGuid(),
            ProcessName = "code",
            TrackingState = TrackingState.Active,
            TotalRunningSeconds = totalRunningSeconds,
            ForegroundSeconds = foregroundSeconds,
        };
    }
}
