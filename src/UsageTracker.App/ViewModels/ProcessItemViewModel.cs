using UsageTracker.Core.Enums;
using UsageTracker.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace UsageTracker.App.ViewModels;

public partial class ProcessItemViewModel : ObservableObject
{
    private readonly Func<Guid, Task> _pauseAsync;
    private readonly Func<Guid, Task> _resumeAsync;
    private readonly Func<Guid, Task> _editAsync;
    private readonly Func<Guid, Task> _removeAsync;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryName))]
    [NotifyPropertyChangedFor(nameof(SecondaryName))]
    private string processName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryName))]
    [NotifyPropertyChangedFor(nameof(SecondaryName))]
    private string? displayName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaused))]
    [NotifyPropertyChangedFor(nameof(StateText))]
    [NotifyPropertyChangedFor(nameof(ActionText))]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorState))]
    private TrackingState trackingState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorState))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private bool isForeground;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayedRunningSeconds))]
    private long totalRunningSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayedForegroundSeconds))]
    private long foregroundSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayedRunningSeconds))]
    private long? filteredRunningSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayedForegroundSeconds))]
    private long? filteredForegroundSeconds;

    [ObservableProperty]
    private long currentSessionRunningSeconds;

    [ObservableProperty]
    private long currentSessionForegroundSeconds;

    public ProcessItemViewModel(
        Guid trackedProcessId,
        Func<Guid, Task> pauseAsync,
        Func<Guid, Task> resumeAsync,
        Func<Guid, Task> editAsync,
        Func<Guid, Task> removeAsync)
    {
        TrackedProcessId = trackedProcessId;
        _pauseAsync = pauseAsync ?? throw new ArgumentNullException(nameof(pauseAsync));
        _resumeAsync = resumeAsync ?? throw new ArgumentNullException(nameof(resumeAsync));
        _editAsync = editAsync ?? throw new ArgumentNullException(nameof(editAsync));
        _removeAsync = removeAsync ?? throw new ArgumentNullException(nameof(removeAsync));

        TogglePauseCommand = new AsyncRelayCommand(TogglePauseAsync);
        EditCommand = new AsyncRelayCommand(EditAsync);
        RemoveCommand = new AsyncRelayCommand(RemoveAsync);
    }

    public Guid TrackedProcessId { get; }

    public string PrimaryName => string.IsNullOrWhiteSpace(DisplayName) ? ProcessName : DisplayName!;

    public string SecondaryName => string.IsNullOrWhiteSpace(DisplayName) ? "No display name" : ProcessName;

    public bool IsPaused => TrackingState == TrackingState.Paused;

    public ProcessItemStatusIndicatorState StatusIndicatorState => IsPaused
        ? ProcessItemStatusIndicatorState.Paused
        : IsRunning
            ? ProcessItemStatusIndicatorState.Running
            : ProcessItemStatusIndicatorState.Idle;

    public string StateText => IsPaused
        ? "Paused"
        : IsForeground
            ? "Foreground"
            : IsRunning
                ? "Running"
                : "Waiting";

    public string ActionText => IsPaused ? "Resume" : "Pause";

    public long DisplayedRunningSeconds => FilteredRunningSeconds ?? TotalRunningSeconds;

    public long DisplayedForegroundSeconds => FilteredForegroundSeconds ?? ForegroundSeconds;

    public IAsyncRelayCommand TogglePauseCommand { get; }

    public IAsyncRelayCommand EditCommand { get; }

    public IAsyncRelayCommand RemoveCommand { get; }

    public void Update(ProcessStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        ProcessName = status.ProcessName;
        DisplayName = status.DisplayName;
        TrackingState = status.TrackingState;
        IsRunning = status.IsRunning;
        IsForeground = status.IsForeground;
        TotalRunningSeconds = status.TotalRunningSeconds;
        ForegroundSeconds = status.ForegroundSeconds;
        CurrentSessionRunningSeconds = status.CurrentSessionRunningSeconds;
        CurrentSessionForegroundSeconds = status.CurrentSessionForegroundSeconds;
    }

    public void SetFilteredTotals(UsageTotals totals)
    {
        ArgumentNullException.ThrowIfNull(totals);

        FilteredRunningSeconds = totals.RunningSeconds;
        FilteredForegroundSeconds = totals.ForegroundSeconds;
    }

    public void ClearFilteredTotals()
    {
        FilteredRunningSeconds = null;
        FilteredForegroundSeconds = null;
    }

    private Task TogglePauseAsync()
    {
        return IsPaused ? _resumeAsync(TrackedProcessId) : _pauseAsync(TrackedProcessId);
    }

    private Task RemoveAsync()
    {
        return _removeAsync(TrackedProcessId);
    }

    private Task EditAsync()
    {
        return _editAsync(TrackedProcessId);
    }
}
