using UsageTracker.App.ViewModels;
using UsageTracker.Core.Interfaces;
using Xunit;

namespace UsageTracker.App.Tests;

public sealed class AddProcessViewModelTests
{
    [Fact]
    public void Constructor_ExcludesAlreadyTrackedProcessNames()
    {
        var viewModel = new AddProcessViewModel(
            new FakeProcessDetector(["chrome", "code", "notepad"]),
            ["code"]);

        Assert.Equal(["chrome", "notepad"], viewModel.FilteredProcesses);
    }

    [Fact]
    public void SearchText_FiltersRunningProcessesCaseInsensitively()
    {
        var viewModel = new AddProcessViewModel(
            new FakeProcessDetector(["chrome", "Code", "notepad"]),
            []);

        viewModel.SearchText = "CO";

        Assert.Equal(["Code"], viewModel.FilteredProcesses);
    }

    [Fact]
    public void SearchText_WhenSelectedProcessIsFilteredOut_ClearsSelection()
    {
        var viewModel = new AddProcessViewModel(
            new FakeProcessDetector(["chrome", "code"]),
            []);

        viewModel.SelectedProcessName = "code";
        viewModel.SearchText = "chrom";

        Assert.Null(viewModel.SelectedProcessName);
        Assert.Equal("chrome", Assert.Single(viewModel.FilteredProcesses));
    }

    [Fact]
    public void ManualProcessName_TakesPrecedenceOverSelectedProcess()
    {
        var viewModel = new AddProcessViewModel(
            new FakeProcessDetector(["chrome"]),
            []);

        viewModel.SelectedProcessName = "chrome";
        viewModel.ManualProcessName = " Code.EXE ";

        Assert.Equal("code", viewModel.ResolvedProcessName);
    }

    [Fact]
    public void TryCreateRequest_NormalizesProcessNameAndTrimsDisplayName()
    {
        var viewModel = new AddProcessViewModel(
            new FakeProcessDetector(["chrome"]),
            []);

        viewModel.ManualProcessName = " Code.EXE ";
        viewModel.DisplayName = "  Visual Studio Code  ";

        var created = viewModel.TryCreateRequest(out var request, out var errorMessage);

        Assert.True(created);
        Assert.Null(errorMessage);
        Assert.NotNull(request);
        Assert.Equal("code", request!.ProcessName);
        Assert.Equal("Visual Studio Code", request.DisplayName);
    }

    [Fact]
    public void TryCreateRequest_BlankDisplayName_ClearsDisplayName()
    {
        var viewModel = new AddProcessViewModel(
            new FakeProcessDetector([]),
            []);

        viewModel.ManualProcessName = "code";
        viewModel.DisplayName = "   ";

        var created = viewModel.TryCreateRequest(out var request, out var errorMessage);

        Assert.True(created);
        Assert.Null(errorMessage);
        Assert.NotNull(request);
        Assert.Null(request!.DisplayName);
    }

    [Fact]
    public void TryCreateRequest_EmptyNormalizedProcess_ReturnsValidationError()
    {
        var viewModel = new AddProcessViewModel(
            new FakeProcessDetector([]),
            []);

        viewModel.ManualProcessName = " .exe ";

        var created = viewModel.TryCreateRequest(out var request, out var errorMessage);

        Assert.False(created);
        Assert.Null(request);
        Assert.Equal("Enter a process name or select one from the running processes list.", errorMessage);
    }

    private sealed class FakeProcessDetector : IProcessDetector
    {
        private readonly IReadOnlySet<string> _processes;

        public FakeProcessDetector(IEnumerable<string> processes)
        {
            _processes = processes.ToHashSet(StringComparer.Ordinal);
        }

        public IReadOnlySet<string> GetRunningProcessNames() => _processes;

        public IReadOnlySet<string> GetRunningTargetProcessNames(IEnumerable<string> targetProcessNames)
        {
            throw new NotSupportedException();
        }
    }
}
