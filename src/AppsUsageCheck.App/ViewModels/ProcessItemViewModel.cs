using AppsUsageCheck.Core.Enums;
using AppsUsageCheck.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppsUsageCheck.App.ViewModels;

public partial class ProcessItemViewModel : ObservableObject
{
    private readonly Func<Guid, Task> _pauseAsync;
    private readonly Func<Guid, Task> _resumeAsync;
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
    private TrackingState trackingState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private bool isForeground;

    [ObservableProperty]
    private long totalRunningSeconds;

    [ObservableProperty]
    private long foregroundSeconds;

    [ObservableProperty]
    private long currentSessionRunningSeconds;

    [ObservableProperty]
    private long currentSessionForegroundSeconds;

    public ProcessItemViewModel(
        Guid trackedProcessId,
        Func<Guid, Task> pauseAsync,
        Func<Guid, Task> resumeAsync,
        Func<Guid, Task> removeAsync)
    {
        TrackedProcessId = trackedProcessId;
        _pauseAsync = pauseAsync ?? throw new ArgumentNullException(nameof(pauseAsync));
        _resumeAsync = resumeAsync ?? throw new ArgumentNullException(nameof(resumeAsync));
        _removeAsync = removeAsync ?? throw new ArgumentNullException(nameof(removeAsync));

        TogglePauseCommand = new AsyncRelayCommand(TogglePauseAsync);
        RemoveCommand = new AsyncRelayCommand(RemoveAsync);
    }

    public Guid TrackedProcessId { get; }

    public string PrimaryName => string.IsNullOrWhiteSpace(DisplayName) ? ProcessName : DisplayName!;

    public string SecondaryName => string.IsNullOrWhiteSpace(DisplayName) ? "No display name" : ProcessName;

    public bool IsPaused => TrackingState == TrackingState.Paused;

    public string StateText => IsPaused
        ? "Paused"
        : IsForeground
            ? "Foreground"
            : IsRunning
                ? "Running"
                : "Waiting";

    public string ActionText => IsPaused ? "Resume" : "Pause";

    public IAsyncRelayCommand TogglePauseCommand { get; }

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

    private Task TogglePauseAsync()
    {
        return IsPaused ? _resumeAsync(TrackedProcessId) : _pauseAsync(TrackedProcessId);
    }

    private Task RemoveAsync()
    {
        return _removeAsync(TrackedProcessId);
    }
}
