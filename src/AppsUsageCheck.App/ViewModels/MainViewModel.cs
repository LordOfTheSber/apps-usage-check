using System.Collections.ObjectModel;
using AppsUsageCheck.App.Services;
using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;

namespace AppsUsageCheck.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ITrackingEngine _trackingEngine;
    private readonly IDialogService _dialogService;
    private readonly TimeProvider _timeProvider;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Dictionary<Guid, ProcessItemViewModel> _itemsById = [];
    private readonly AsyncRelayCommand _pauseAllCommand;
    private readonly AsyncRelayCommand _resumeAllCommand;
    private bool _isInitialized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProcesses))]
    private string statusMessage = "Loading tracked processes...";

    [ObservableProperty]
    private DateTimeOffset lastRefreshedAt;

    public MainViewModel(ITrackingEngine trackingEngine, IDialogService dialogService, TimeProvider timeProvider)
    {
        _trackingEngine = trackingEngine ?? throw new ArgumentNullException(nameof(trackingEngine));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        Processes = [];
        AddProcessCommand = new AsyncRelayCommand(AddProcessAsync);
        _pauseAllCommand = new AsyncRelayCommand(PauseAllAsync, CanPauseAll);
        _resumeAllCommand = new AsyncRelayCommand(ResumeAllAsync, CanResumeAll);
        PauseAllCommand = _pauseAllCommand;
        ResumeAllCommand = _resumeAllCommand;
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ExitCommand = new RelayCommand(RequestExit);
        RefreshCommand = new RelayCommand(RefreshStatuses);

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _refreshTimer.Tick += OnRefreshTimerTick;
    }

    public event EventHandler? ExitRequested;

    public ObservableCollection<ProcessItemViewModel> Processes { get; }

    public bool HasProcesses => Processes.Count > 0;

    public int TrackedProcessCount => Processes.Count;

    public int ActiveProcessCount => Processes.Count(process => !process.IsPaused);

    public int RunningProcessCount => Processes.Count(process => process.IsRunning);

    public string TrayToolTipText => $"Tracking {TrackedProcessCount} processes | {ActiveProcessCount} active";

    public IAsyncRelayCommand AddProcessCommand { get; }

    public IAsyncRelayCommand PauseAllCommand { get; }

    public IAsyncRelayCommand ResumeAllCommand { get; }

    public IRelayCommand OpenSettingsCommand { get; }

    public IRelayCommand ExitCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    public Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return Task.CompletedTask;
        }

        _isInitialized = true;
        RefreshStatuses();
        _refreshTimer.Start();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
    }

    private async Task AddProcessAsync()
    {
        try
        {
            var trackedProcessNames = _trackingEngine.GetAllStatuses()
                .Select(status => status.ProcessName)
                .ToArray();

            var request = await _dialogService.ShowAddProcessDialogAsync(trackedProcessNames).ConfigureAwait(true);
            if (request is null)
            {
                return;
            }

            await _trackingEngine.AddTrackedProcessAsync(request.ProcessName, request.DisplayName).ConfigureAwait(true);
            RefreshStatuses();
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to add process", exception.Message);
        }
    }

    private void OpenSettings()
    {
        _dialogService.ShowInformation("Settings", "Settings UI is planned for Phase 8.");
    }

    private void RequestExit()
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task PauseAllAsync()
    {
        try
        {
            await _trackingEngine.PauseAllTrackingAsync().ConfigureAwait(true);
            RefreshStatuses();
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to pause tracking", exception.Message);
        }
    }

    private async Task ResumeAllAsync()
    {
        try
        {
            await _trackingEngine.ResumeAllTrackingAsync().ConfigureAwait(true);
            RefreshStatuses();
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to resume tracking", exception.Message);
        }
    }

    private async Task PauseTrackingAsync(Guid trackedProcessId)
    {
        try
        {
            await _trackingEngine.PauseTrackingAsync(trackedProcessId).ConfigureAwait(true);
            RefreshStatuses();
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to pause tracking", exception.Message);
        }
    }

    private async Task ResumeTrackingAsync(Guid trackedProcessId)
    {
        try
        {
            await _trackingEngine.ResumeTrackingAsync(trackedProcessId).ConfigureAwait(true);
            RefreshStatuses();
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to resume tracking", exception.Message);
        }
    }

    private async Task RemoveTrackedProcessAsync(Guid trackedProcessId)
    {
        var process = Processes.FirstOrDefault(item => item.TrackedProcessId == trackedProcessId);
        var processLabel = process?.PrimaryName ?? "this process";

        if (!_dialogService.Confirm("Remove tracked process", $"Stop tracking {processLabel}?"))
        {
            return;
        }

        try
        {
            await _trackingEngine.RemoveTrackedProcessAsync(trackedProcessId).ConfigureAwait(true);
            RefreshStatuses();
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to remove process", exception.Message);
        }
    }

    private void RefreshStatuses()
    {
        var statuses = _trackingEngine.GetAllStatuses()
            .OrderBy(status => status.DisplayName ?? status.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(status => status.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var activeIds = statuses.Select(status => status.TrackedProcessId).ToHashSet();
        foreach (var missingId in _itemsById.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            _itemsById.Remove(missingId);
        }

        var orderedItems = new List<ProcessItemViewModel>(statuses.Length);
        foreach (var status in statuses)
        {
            if (!_itemsById.TryGetValue(status.TrackedProcessId, out var item))
            {
                item = new ProcessItemViewModel(
                    status.TrackedProcessId,
                    PauseTrackingAsync,
                    ResumeTrackingAsync,
                    RemoveTrackedProcessAsync);
                _itemsById.Add(status.TrackedProcessId, item);
            }

            item.Update(status);
            orderedItems.Add(item);
        }

        RebuildCollection(orderedItems);

        LastRefreshedAt = _timeProvider.GetLocalNow();
        StatusMessage = statuses.Length == 0
            ? "No tracked processes yet. Add a running app or enter a process name manually."
            : $"Tracking {TrackedProcessCount} process(es). {RunningProcessCount} running right now.";

        OnPropertyChanged(nameof(HasProcesses));
        OnPropertyChanged(nameof(TrackedProcessCount));
        OnPropertyChanged(nameof(ActiveProcessCount));
        OnPropertyChanged(nameof(RunningProcessCount));
        OnPropertyChanged(nameof(TrayToolTipText));
        _pauseAllCommand.NotifyCanExecuteChanged();
        _resumeAllCommand.NotifyCanExecuteChanged();
    }

    private void RebuildCollection(IReadOnlyList<ProcessItemViewModel> orderedItems)
    {
        if (Processes.Count == orderedItems.Count && Processes.SequenceEqual(orderedItems))
        {
            return;
        }

        Processes.Clear();
        foreach (var item in orderedItems)
        {
            Processes.Add(item);
        }
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        RefreshStatuses();
    }

    private bool CanPauseAll()
    {
        return Processes.Any(process => !process.IsPaused);
    }

    private bool CanResumeAll()
    {
        return Processes.Any(process => process.IsPaused);
    }
}
