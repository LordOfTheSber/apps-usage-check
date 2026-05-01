using UsageTracker.App.Models;
using UsageTracker.App.ViewModels;
using UsageTracker.Core.Enums;
using UsageTracker.Core.Models;
using Xunit;

namespace UsageTracker.App.Tests;

public sealed class RenameProcessViewModelTests
{
    [Fact]
    public void Constructor_InitializesFromStatus()
    {
        var status = new ProcessStatus
        {
            TrackedProcessId = Guid.NewGuid(),
            ProcessName = "code",
            DisplayName = "Visual Studio Code",
            TrackingState = TrackingState.Active,
        };

        var viewModel = new RenameProcessViewModel(status);

        Assert.Equal("code", viewModel.ProcessName);
        Assert.Equal("Visual Studio Code", viewModel.DisplayName);
        Assert.Equal("Visual Studio Code", viewModel.OriginalDisplayName);
        Assert.False(viewModel.CanSubmit);
    }

    [Fact]
    public void CanSubmit_FalseWhenChangeIsOnlyWhitespaceAroundExistingName()
    {
        var status = new ProcessStatus
        {
            TrackedProcessId = Guid.NewGuid(),
            ProcessName = "code",
            DisplayName = "Visual Studio Code",
            TrackingState = TrackingState.Active,
        };
        var viewModel = new RenameProcessViewModel(status);

        viewModel.DisplayName = "  Visual Studio Code  ";

        Assert.False(viewModel.CanSubmit);
    }

    [Fact]
    public void TryCreateRequest_BlankDisplayNameClearsCustomName()
    {
        var status = new ProcessStatus
        {
            TrackedProcessId = Guid.NewGuid(),
            ProcessName = "code",
            DisplayName = "Visual Studio Code",
            TrackingState = TrackingState.Active,
        };
        var viewModel = new RenameProcessViewModel(status)
        {
            DisplayName = "   ",
        };

        var created = viewModel.TryCreateRequest(out var request, out var errorMessage);

        Assert.True(created);
        Assert.Null(errorMessage);
        Assert.Equal(new RenameProcessRequest(null), request);
    }
}
