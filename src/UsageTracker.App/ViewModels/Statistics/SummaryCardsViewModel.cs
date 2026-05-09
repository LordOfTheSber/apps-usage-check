using CommunityToolkit.Mvvm.ComponentModel;

namespace UsageTracker.App.ViewModels.Statistics;

public partial class SummaryCardsViewModel : ObservableObject
{
    [ObservableProperty]
    private string mostUsedProcessName = "—";

    [ObservableProperty]
    private long mostUsedSeconds;

    [ObservableProperty]
    private long totalTrackedSeconds;

    [ObservableProperty]
    private int activeProcessCount;

    public bool HasData => TotalTrackedSeconds > 0;

    public void Update(
        IReadOnlyDictionary<Guid, long> runningSecondsByProcess,
        IReadOnlyDictionary<Guid, string> displayNamesByProcess)
    {
        var total = 0L;
        var topProcessId = Guid.Empty;
        var topSeconds = 0L;
        var active = 0;

        foreach (var (id, seconds) in runningSecondsByProcess)
        {
            total += seconds;
            if (seconds > 0L)
            {
                active++;
            }

            if (seconds > topSeconds)
            {
                topSeconds = seconds;
                topProcessId = id;
            }
        }

        TotalTrackedSeconds = total;
        ActiveProcessCount = active;
        MostUsedSeconds = topSeconds;
        MostUsedProcessName = topSeconds > 0L && displayNamesByProcess.TryGetValue(topProcessId, out var name)
            ? name
            : "—";

        OnPropertyChanged(nameof(HasData));
    }
}
