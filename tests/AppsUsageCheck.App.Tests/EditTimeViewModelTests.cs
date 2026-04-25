using AppsUsageCheck.App.ViewModels;
using AppsUsageCheck.Core.Enums;
using AppsUsageCheck.Core.Models;
using Xunit;

namespace AppsUsageCheck.App.Tests;

public sealed class EditTimeViewModelTests
{
    [Fact]
    public void ZeroAdjustment_HasNoValidationErrorAndNoPendingChange()
    {
        var viewModel = new EditTimeViewModel(CreateStatus());

        Assert.False(viewModel.HasPendingChange);
        Assert.False(viewModel.HasValidationError);
        Assert.False(viewModel.CanSubmit);
    }

    [Fact]
    public void NonZeroAdjustment_FlipsHasPendingChangeAndCanSubmit()
    {
        var viewModel = new EditTimeViewModel(CreateStatus(totalRunningSeconds: 600));

        viewModel.MinutesText = "5";

        Assert.True(viewModel.HasPendingChange);
        Assert.False(viewModel.HasValidationError);
        Assert.True(viewModel.CanSubmit);
    }

    [Fact]
    public void TryCreateRequest_RejectsZeroAdjustment()
    {
        var viewModel = new EditTimeViewModel(CreateStatus());

        var created = viewModel.TryCreateRequest(out var request, out var error);

        Assert.False(created);
        Assert.Null(request);
        Assert.Equal("Enter a non-zero adjustment.", error);
    }

    [Fact]
    public void TryCreateRequest_BuildsSignedRequest()
    {
        var viewModel = new EditTimeViewModel(CreateStatus(totalRunningSeconds: 600));

        viewModel.MinutesText = "2";
        viewModel.SelectedOperationOption = viewModel.OperationOptions.Single(option => option.IsSubtract);

        var created = viewModel.TryCreateRequest(out var request, out var error);

        Assert.True(created);
        Assert.Null(error);
        Assert.NotNull(request);
        Assert.Equal(TimeAdjustmentTarget.Running, request!.Target);
        Assert.Equal(-120L, request.AdjustmentSeconds);
    }

    private static ProcessStatus CreateStatus(long totalRunningSeconds = 0)
    {
        return new ProcessStatus
        {
            TrackedProcessId = Guid.NewGuid(),
            ProcessName = "code",
            TrackingState = TrackingState.Active,
            TotalRunningSeconds = totalRunningSeconds,
        };
    }
}
