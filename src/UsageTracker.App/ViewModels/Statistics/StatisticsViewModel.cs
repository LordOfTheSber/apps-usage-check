using UsageTracker.Core.Interfaces;
using UsageTracker.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UsageTracker.App.ViewModels.Statistics;

public partial class StatisticsViewModel : ObservableObject
{
    private readonly ITrackingEngine _trackingEngine;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveSubViewModel))]
    [NotifyPropertyChangedFor(nameof(IsSummarySelected))]
    [NotifyPropertyChangedFor(nameof(IsPerProcessSelected))]
    [NotifyPropertyChangedFor(nameof(IsDailySelected))]
    [NotifyPropertyChangedFor(nameof(IsShareSelected))]
    private StatisticKind selectedStat = StatisticKind.Summary;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool hasAnyData;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    private TimeRange? _cachedRange;
    private TimeRange? _cachedDailyRange;
    private IReadOnlyList<DailyUsageBucket>? _cachedDaily;
    private int _dailyRequestId;
    private IReadOnlyDictionary<Guid, long> _cachedRunning = new Dictionary<Guid, long>();
    private IReadOnlyDictionary<Guid, long> _cachedForeground = new Dictionary<Guid, long>();
    private IReadOnlyDictionary<Guid, string> _cachedDisplayNames = new Dictionary<Guid, string>();

    public StatisticsViewModel(ITrackingEngine trackingEngine)
    {
        _trackingEngine = trackingEngine ?? throw new ArgumentNullException(nameof(trackingEngine));
    }

    public SummaryCardsViewModel Summary { get; } = new();

    public PerProcessTotalsViewModel PerProcess { get; } = new();

    public DailyBreakdownViewModel Daily { get; } = new();

    public ShareBreakdownViewModel Share { get; } = new();

    public object ActiveSubViewModel => SelectedStat switch
    {
        StatisticKind.PerProcess => PerProcess,
        StatisticKind.Daily => Daily,
        StatisticKind.Share => Share,
        _ => Summary,
    };

    public bool IsSummarySelected
    {
        get => SelectedStat == StatisticKind.Summary;
        set
        {
            if (value)
            {
                SelectedStat = StatisticKind.Summary;
            }
        }
    }

    public bool IsPerProcessSelected
    {
        get => SelectedStat == StatisticKind.PerProcess;
        set
        {
            if (value)
            {
                SelectedStat = StatisticKind.PerProcess;
            }
        }
    }

    public bool IsDailySelected
    {
        get => SelectedStat == StatisticKind.Daily;
        set
        {
            if (value)
            {
                SelectedStat = StatisticKind.Daily;
            }
        }
    }

    public bool IsShareSelected
    {
        get => SelectedStat == StatisticKind.Share;
        set
        {
            if (value)
            {
                SelectedStat = StatisticKind.Share;
            }
        }
    }

    // Called every MainViewModel tick. Updates the cache silently — no charts redraw on the tick itself.
    // Sub-views are only re-rendered on explicit user intent: tab activation, stat selection, range change.
    public void OnRefreshed(
        IReadOnlyDictionary<Guid, UsageTotals>? filteredTotals,
        IReadOnlyList<ProcessStatus> statuses,
        TimeRange range)
    {
        var rangeChanged = _cachedRange is null || _cachedRange != range;

        var displayNames = BuildDisplayNames(statuses);
        var (running, foreground) = BuildSecondsByProcess(filteredTotals, statuses);

        _cachedRange = range;
        _cachedRunning = running;
        _cachedForeground = foreground;
        _cachedDisplayNames = displayNames;

        if (rangeChanged)
        {
            _cachedDaily = null;
            _cachedDailyRange = null;
        }

        HasAnyData = running.Values.Any(v => v > 0L);
        StatusMessage = IsAllTime(range)
            ? (HasAnyData ? "Showing all-time totals." : "No usage recorded yet.")
            : (HasAnyData ? "Showing the selected range." : "No usage recorded in this range.");

        if (rangeChanged && IsActive)
        {
            _ = RenderActiveAsync();
        }
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (value)
        {
            _ = RenderActiveAsync();
        }
    }

    partial void OnSelectedStatChanged(StatisticKind value)
    {
        if (IsActive)
        {
            _ = RenderActiveAsync();
        }
    }

    private async Task RenderActiveAsync()
    {
        if (!IsActive || _cachedRange is not { } range)
        {
            return;
        }

        switch (SelectedStat)
        {
            case StatisticKind.PerProcess:
                PerProcess.Update(_cachedRunning, _cachedForeground, _cachedDisplayNames);
                break;

            case StatisticKind.Share:
                Share.Update(_cachedRunning, _cachedDisplayNames);
                break;

            case StatisticKind.Daily:
                if (IsAllTime(range))
                {
                    Daily.Clear("Select a non-'All time' range to see daily breakdown.");
                }
                else
                {
                    await EnsureDailyAsync(range).ConfigureAwait(true);
                    if (_cachedDaily is not null && _cachedDailyRange == range)
                    {
                        Daily.Update(_cachedDaily, _cachedDisplayNames);
                    }
                }
                break;

            case StatisticKind.Summary:
            default:
                Summary.Update(_cachedRunning, _cachedDisplayNames);
                break;
        }
    }

    private async Task EnsureDailyAsync(TimeRange range)
    {
        if (_cachedDaily is not null && _cachedDailyRange == range)
        {
            return;
        }

        var requestId = Interlocked.Increment(ref _dailyRequestId);

        try
        {
            var buckets = await _trackingEngine
                .GetDailyUsageAsync(range.From, range.To)
                .ConfigureAwait(true);

            if (requestId != _dailyRequestId || _cachedRange != range)
            {
                return;
            }

            _cachedDaily = buckets;
            _cachedDailyRange = range;
        }
        catch (Exception)
        {
            if (requestId == _dailyRequestId)
            {
                Daily.Clear("Unable to load daily breakdown.");
            }
        }
    }

    private static (IReadOnlyDictionary<Guid, long> Running, IReadOnlyDictionary<Guid, long> Foreground)
        BuildSecondsByProcess(
            IReadOnlyDictionary<Guid, UsageTotals>? filteredTotals,
            IReadOnlyList<ProcessStatus> statuses)
    {
        var running = new Dictionary<Guid, long>(statuses.Count);
        var foreground = new Dictionary<Guid, long>(statuses.Count);

        foreach (var status in statuses)
        {
            if (filteredTotals is not null && filteredTotals.TryGetValue(status.TrackedProcessId, out var totals))
            {
                running[status.TrackedProcessId] = totals.RunningSeconds;
                foreground[status.TrackedProcessId] = totals.ForegroundSeconds;
            }
            else
            {
                running[status.TrackedProcessId] = status.TotalRunningSeconds;
                foreground[status.TrackedProcessId] = status.ForegroundSeconds;
            }
        }

        return (running, foreground);
    }

    private static IReadOnlyDictionary<Guid, string> BuildDisplayNames(IReadOnlyList<ProcessStatus> statuses)
    {
        var displayNames = new Dictionary<Guid, string>(statuses.Count);
        foreach (var status in statuses)
        {
            displayNames[status.TrackedProcessId] = string.IsNullOrWhiteSpace(status.DisplayName)
                ? status.ProcessName
                : status.DisplayName!;
        }

        return displayNames;
    }

    private static bool IsAllTime(TimeRange range)
    {
        return range.From == DateTimeOffset.MinValue;
    }
}
