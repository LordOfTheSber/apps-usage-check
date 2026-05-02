using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using UsageTracker.App.Services;
using UsageTracker.Core.Enums;
using UsageTracker.Core.Interfaces;
using UsageTracker.Core.Models;
using UsageTracker.Infrastructure.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace UsageTracker.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan LiveFilteredRefreshInterval = TimeSpan.FromSeconds(5);

    private readonly ITrackingEngine _trackingEngine;
    private readonly IDialogService _dialogService;
    private readonly TimeProvider _timeProvider;
    private readonly IDatabaseHealthCheck _databaseHealthCheck;
    private readonly IProcessIconService? _iconService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Dictionary<Guid, ProcessItemViewModel> _itemsById = [];
    private readonly HashSet<Guid> _filteredTotalsProcessIds = [];
    private readonly AsyncRelayCommand _pauseAllCommand;
    private readonly AsyncRelayCommand _resumeAllCommand;
    private bool _isInitialized;
    private bool _isRefreshing;
    private bool _isNormalizingCustomRange;
    private TimeRange? _lastAppliedTimeRange;
    private DateTimeOffset? _nextFilteredRefreshAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProcesses))]
    private string statusMessage = "Loading tracked processes...";

    [ObservableProperty]
    private DateTimeOffset lastRefreshedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomRangeSelected))]
    private TimeRangePreset selectedPreset = TimeRangePreset.AllTime;

    [ObservableProperty]
    private DateTime? customStartDate;

    [ObservableProperty]
    private DateTime? customEndDate;

    [ObservableProperty]
    private DatabaseConnectionState databaseConnectionState;

    [ObservableProperty]
    private string databaseStatusText = "Database status pending...";

    public ProcessGridSortColumn CurrentSortColumn { get; private set; } = ProcessGridSortColumn.Process;

    public ListSortDirection CurrentSortDirection { get; private set; } = ListSortDirection.Ascending;

    public MainViewModel(
        ITrackingEngine trackingEngine,
        IDialogService dialogService,
        TimeProvider timeProvider,
        IDatabaseHealthCheck databaseHealthCheck,
        IProcessIconService? iconService = null)
    {
        _trackingEngine = trackingEngine ?? throw new ArgumentNullException(nameof(trackingEngine));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _databaseHealthCheck = databaseHealthCheck ?? throw new ArgumentNullException(nameof(databaseHealthCheck));
        _iconService = iconService;

        if (_iconService is not null)
        {
            _iconService.IconRefreshed += OnIconRefreshed;
        }

        var today = _timeProvider.GetLocalNow().Date;

        Processes = [];
        CustomStartDate = today;
        CustomEndDate = today;
        AddProcessCommand = new AsyncRelayCommand(AddProcessAsync);
        _pauseAllCommand = new AsyncRelayCommand(PauseAllAsync, CanPauseAll);
        _resumeAllCommand = new AsyncRelayCommand(ResumeAllAsync, CanResumeAll);
        PauseAllCommand = _pauseAllCommand;
        ResumeAllCommand = _resumeAllCommand;
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ExitCommand = new RelayCommand(RequestExit);
        RefreshCommand = new RelayCommand(ForceRefresh);

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _refreshTimer.Tick += OnRefreshTimerTick;
        UpdateDatabaseStatus();
    }

    public event EventHandler? ExitRequested;

    public ObservableCollection<ProcessItemViewModel> Processes { get; }

    public bool HasProcesses => Processes.Count > 0;

    public bool IsCustomRangeSelected => SelectedPreset == TimeRangePreset.Custom;

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

    public ListSortDirection ApplySort(ProcessGridSortColumn sortColumn)
    {
        CurrentSortDirection = CurrentSortColumn == sortColumn
            ? ToggleSortDirection(CurrentSortDirection)
            : ListSortDirection.Ascending;
        CurrentSortColumn = sortColumn;

        RebuildCollection(ProcessGridSorter.OrderItems(Processes, CurrentSortColumn, CurrentSortDirection));
        return CurrentSortDirection;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        UpdateDatabaseStatus();
        await RefreshStatusesAsync(forceFilteredTotalsRefresh: true).ConfigureAwait(true);
        _refreshTimer.Start();
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        if (_iconService is not null)
        {
            _iconService.IconRefreshed -= OnIconRefreshed;
        }
    }

    partial void OnSelectedPresetChanged(TimeRangePreset value)
    {
        if (value == TimeRangePreset.Custom)
        {
            EnsureCustomDatesInitialized();
        }

        ResetFilteredRefreshState();

        if (_isInitialized)
        {
            RequestRefresh(forceFilteredTotalsRefresh: true);
        }
    }

    partial void OnCustomStartDateChanged(DateTime? value)
    {
        NormalizeCustomRange(changedStart: true);
        OnCustomRangeChanged();
    }

    partial void OnCustomEndDateChanged(DateTime? value)
    {
        NormalizeCustomRange(changedStart: false);
        OnCustomRangeChanged();
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
            await RefreshStatusesAsync(forceFilteredTotalsRefresh: true).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to add process", exception.Message);
        }
    }

    private void OpenSettings()
    {
        try
        {
            _dialogService.ShowSettingsDialog();
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to open settings", exception.Message);
        }
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
            await RefreshStatusesAsync(forceFilteredTotalsRefresh: true).ConfigureAwait(true);
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
            await RefreshStatusesAsync(forceFilteredTotalsRefresh: true).ConfigureAwait(true);
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
            await RefreshStatusesAsync(forceFilteredTotalsRefresh: true).ConfigureAwait(true);
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
            await RefreshStatusesAsync(forceFilteredTotalsRefresh: true).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to resume tracking", exception.Message);
        }
    }

    private async Task EditProcessAsync(Guid trackedProcessId)
    {
        try
        {
            var status = _trackingEngine.GetAllStatuses()
                .FirstOrDefault(candidate => candidate.TrackedProcessId == trackedProcessId);

            if (status is null)
            {
                _dialogService.ShowError("Unable to edit process", "The selected process is no longer available.");
                await RefreshStatusesAsync(forceFilteredTotalsRefresh: true).ConfigureAwait(true);
                return;
            }

            var result = await _dialogService.ShowEditProcessDialogAsync(status).ConfigureAwait(true);
            if (result is null)
            {
                return;
            }

            if (result.Rename is { } rename)
            {
                await _trackingEngine
                    .UpdateTrackedProcessDisplayNameAsync(trackedProcessId, rename.DisplayName)
                    .ConfigureAwait(true);
            }

            if (result.TimeAdjustment is { } adjustment)
            {
                await _trackingEngine
                    .ApplyTimeAdjustmentAsync(trackedProcessId, adjustment.Target, adjustment.AdjustmentSeconds, adjustment.Reason)
                    .ConfigureAwait(true);
            }

            await RefreshStatusesAsync(forceFilteredTotalsRefresh: true).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to edit process", exception.Message);
        }
    }

    private async Task RefreshIconAsync(Guid trackedProcessId)
    {
        if (_iconService is null)
        {
            return;
        }

        try
        {
            var item = Processes.FirstOrDefault(candidate => candidate.TrackedProcessId == trackedProcessId);
            if (item is null)
            {
                return;
            }

            var extracted = await _iconService.TryExtractAndSaveAsync(trackedProcessId, item.ProcessName).ConfigureAwait(true);
            if (!extracted)
            {
                _dialogService.ShowError(
                    "Unable to refresh icon",
                    $"Could not extract an icon for '{item.ProcessName}'. Make sure the process is currently running. " +
                    "Some system or elevated processes also block access to their executable path — see the application log for details.");
            }
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to refresh icon", exception.Message);
        }
    }

    // IconRefreshed fires off the UI thread; marshal before touching bound state.
    private void OnIconRefreshed(object? sender, IconRefreshedEventArgs e)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (_itemsById.TryGetValue(e.TrackedProcessId, out var item))
            {
                item.IconSource = LoadIconFromDisk(e.ProcessName);
            }
        });
    }

    private ImageSource? LoadIconFromDisk(string processName)
    {
        if (_iconService is null)
        {
            return null;
        }

        var path = _iconService.GetIconFilePath(processName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
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
            await RefreshStatusesAsync(forceFilteredTotalsRefresh: true).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _dialogService.ShowError("Unable to remove process", exception.Message);
        }
    }

    internal async Task RefreshStatusesAsync(bool forceFilteredTotalsRefresh = false)
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;

        try
        {
            UpdateDatabaseStatus();
            var nowLocal = _timeProvider.GetLocalNow();
            var statuses = _trackingEngine.GetAllStatuses().ToArray();
            var activeIds = statuses.Select(status => status.TrackedProcessId).ToHashSet();
            var filteredTotals = await GetFilteredTotalsIfNeededAsync(activeIds, nowLocal, forceFilteredTotalsRefresh).ConfigureAwait(true);

            foreach (var missingId in _itemsById.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                _itemsById.Remove(missingId);
                _filteredTotalsProcessIds.Remove(missingId);
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
                        EditProcessAsync,
                        RemoveTrackedProcessAsync,
                        _iconService is null ? null : RefreshIconAsync);
                    _itemsById.Add(status.TrackedProcessId, item);

                    if (_iconService is not null)
                    {
                        item.IconSource = LoadIconFromDisk(status.ProcessName);
                    }
                }

                item.Update(status);

                if (SelectedPreset == TimeRangePreset.AllTime)
                {
                    item.ClearFilteredTotals();
                }
                else if (filteredTotals is not null)
                {
                    item.SetFilteredTotals(
                        filteredTotals.TryGetValue(status.TrackedProcessId, out var totals)
                            ? totals
                            : new UsageTotals(0L, 0L));
                }

                orderedItems.Add(item);
            }

            RebuildCollection(ProcessGridSorter.OrderItems(orderedItems, CurrentSortColumn, CurrentSortDirection));

            LastRefreshedAt = nowLocal;
            StatusMessage = BuildStatusMessage();

            OnPropertyChanged(nameof(HasProcesses));
            OnPropertyChanged(nameof(TrackedProcessCount));
            OnPropertyChanged(nameof(ActiveProcessCount));
            OnPropertyChanged(nameof(RunningProcessCount));
            OnPropertyChanged(nameof(TrayToolTipText));
            _pauseAllCommand.NotifyCanExecuteChanged();
            _resumeAllCommand.NotifyCanExecuteChanged();
        }
        catch (Exception exception)
        {
            LastRefreshedAt = _timeProvider.GetLocalNow();
            StatusMessage = $"Unable to refresh usage data: {exception.Message}";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task<IReadOnlyDictionary<Guid, UsageTotals>?> GetFilteredTotalsIfNeededAsync(
        HashSet<Guid> activeIds,
        DateTimeOffset nowLocal,
        bool forceFilteredTotalsRefresh)
    {
        if (SelectedPreset == TimeRangePreset.AllTime)
        {
            ResetFilteredRefreshState();
            return null;
        }

        var range = BuildSelectedTimeRange(nowLocal);
        var shouldRefreshFilteredTotals =
            forceFilteredTotalsRefresh
            || _lastAppliedTimeRange != range
            || !_filteredTotalsProcessIds.SetEquals(activeIds)
            || (range.IsLiveAt(nowLocal) && (!_nextFilteredRefreshAt.HasValue || nowLocal >= _nextFilteredRefreshAt.Value));

        if (!shouldRefreshFilteredTotals)
        {
            return null;
        }

        var filteredTotals = await _trackingEngine
            .GetFilteredTotalsAsync(range.From, range.To)
            .ConfigureAwait(true);

        _lastAppliedTimeRange = range;
        _filteredTotalsProcessIds.Clear();
        _filteredTotalsProcessIds.UnionWith(activeIds);
        _nextFilteredRefreshAt = range.IsLiveAt(nowLocal)
            ? nowLocal.Add(LiveFilteredRefreshInterval)
            : null;

        return filteredTotals;
    }

    private TimeRange BuildSelectedTimeRange(DateTimeOffset nowLocal)
    {
        return TimeRange.Create(
            SelectedPreset,
            nowLocal,
            _timeProvider.LocalTimeZone,
            CustomStartDate,
            CustomEndDate);
    }

    private string BuildStatusMessage()
    {
        if (TrackedProcessCount == 0)
        {
            return "No tracked processes yet. Add a running app or enter a process name manually.";
        }

        if (SelectedPreset == TimeRangePreset.AllTime)
        {
            return $"Tracking {TrackedProcessCount} process(es). {RunningProcessCount} running right now.";
        }

        return $"Showing {GetSelectedRangeLabel()}. Tracking {TrackedProcessCount} process(es); {RunningProcessCount} running right now.";
    }

    private string GetSelectedRangeLabel()
    {
        return SelectedPreset switch
        {
            TimeRangePreset.Today => "today's usage",
            TimeRangePreset.Yesterday => "yesterday's usage",
            TimeRangePreset.Last3Days => "the last 3 days",
            TimeRangePreset.Last7Days => "the last 7 days",
            TimeRangePreset.Last14Days => "the last 14 days",
            TimeRangePreset.Last30Days => "the last 30 days",
            TimeRangePreset.ThisWeek => "this week's usage",
            TimeRangePreset.LastWeek => "last week's usage",
            TimeRangePreset.ThisMonth => "this month's usage",
            TimeRangePreset.LastMonth => "last month's usage",
            TimeRangePreset.Custom => $"usage from {CustomStartDate:yyyy-MM-dd} to {CustomEndDate:yyyy-MM-dd}",
            _ => "all usage",
        };
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
        UpdateDatabaseStatus();
        RequestRefresh();
    }

    private void ForceRefresh()
    {
        RequestRefresh(forceFilteredTotalsRefresh: true);
    }

    private void OnCustomRangeChanged()
    {
        if (!_isInitialized || _isNormalizingCustomRange || SelectedPreset != TimeRangePreset.Custom)
        {
            return;
        }

        ResetFilteredRefreshState();
        RequestRefresh(forceFilteredTotalsRefresh: true);
    }

    private void RequestRefresh(bool forceFilteredTotalsRefresh = false)
    {
        _ = RefreshStatusesAsync(forceFilteredTotalsRefresh);
    }

    private void NormalizeCustomRange(bool changedStart)
    {
        if (_isNormalizingCustomRange)
        {
            return;
        }

        _isNormalizingCustomRange = true;

        try
        {
            if (!CustomStartDate.HasValue && CustomEndDate.HasValue)
            {
                CustomStartDate = CustomEndDate.Value.Date;
            }
            else if (!CustomEndDate.HasValue && CustomStartDate.HasValue)
            {
                CustomEndDate = CustomStartDate.Value.Date;
            }
            else if (CustomStartDate.HasValue && CustomEndDate.HasValue && CustomStartDate.Value.Date > CustomEndDate.Value.Date)
            {
                if (changedStart)
                {
                    CustomEndDate = CustomStartDate.Value.Date;
                }
                else
                {
                    CustomStartDate = CustomEndDate.Value.Date;
                }
            }
        }
        finally
        {
            _isNormalizingCustomRange = false;
        }
    }

    private void EnsureCustomDatesInitialized()
    {
        var today = _timeProvider.GetLocalNow().Date;

        if (!CustomStartDate.HasValue)
        {
            CustomStartDate = today;
        }

        if (!CustomEndDate.HasValue)
        {
            CustomEndDate = CustomStartDate ?? today;
        }
    }

    private void ResetFilteredRefreshState()
    {
        _lastAppliedTimeRange = null;
        _nextFilteredRefreshAt = null;
        _filteredTotalsProcessIds.Clear();
    }

    private bool CanPauseAll()
    {
        return Processes.Any(process => !process.IsPaused);
    }

    private bool CanResumeAll()
    {
        return Processes.Any(process => process.IsPaused);
    }

    private void UpdateDatabaseStatus()
    {
        DatabaseConnectionState = _databaseHealthCheck.ConnectionState;
        DatabaseStatusText = _databaseHealthCheck.StatusText;
    }

    private static ListSortDirection ToggleSortDirection(ListSortDirection direction)
    {
        return direction == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
    }
}
