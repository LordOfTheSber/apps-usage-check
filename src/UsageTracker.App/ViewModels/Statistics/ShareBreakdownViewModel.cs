using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UsageTracker.App.ViewModels.Statistics;

public partial class ShareBreakdownViewModel : ObservableObject
{
    private const int TopSliceCount = 8;
    private static readonly SKColor OtherColor = new(0xB7, 0xAE, 0x9E);

    [ObservableProperty]
    private bool hasData;

    [ObservableProperty]
    private string totalLabel = string.Empty;

    public ISeries[] Series { get; private set; } = Array.Empty<ISeries>();

    public void Update(
        IReadOnlyDictionary<Guid, long> runningSecondsByProcess,
        IReadOnlyDictionary<Guid, string> displayNamesByProcess)
    {
        var ordered = runningSecondsByProcess
            .Where(kvp => kvp.Value > 0L)
            .OrderByDescending(kvp => kvp.Value)
            .ToArray();

        if (ordered.Length == 0)
        {
            Series = Array.Empty<ISeries>();
            HasData = false;
            TotalLabel = string.Empty;
            OnPropertyChanged(nameof(Series));
            return;
        }

        var totalSeconds = ordered.Sum(kvp => kvp.Value);
        var totalHours = totalSeconds / 3600.0;
        TotalLabel = $"Total {FormatHours(totalHours)}";

        var top = ordered.Take(TopSliceCount).ToArray();
        var otherSeconds = ordered.Skip(TopSliceCount).Sum(kvp => kvp.Value);

        var slices = new List<(string Name, long Seconds, SKColor Color)>(top.Length + 1);
        foreach (var kvp in top)
        {
            var name = displayNamesByProcess.TryGetValue(kvp.Key, out var label) ? label : "—";
            slices.Add((name, kvp.Value, StatisticsPalette.For(kvp.Key)));
        }

        if (otherSeconds > 0L)
        {
            slices.Add(("Other", otherSeconds, OtherColor));
        }

        var series = new List<ISeries>(slices.Count);
        foreach (var slice in slices)
        {
            var sliceHours = slice.Seconds / 3600.0;
            var pct = totalSeconds > 0L ? slice.Seconds * 100.0 / totalSeconds : 0.0;
            var name = slice.Name;

            series.Add(new PieSeries<double>
            {
                Values = new[] { sliceHours },
                Name = name,
                Fill = new SolidColorPaint(slice.Color),
                Stroke = null,
                InnerRadius = 70,
                MaxRadialColumnWidth = 60,
                ToolTipLabelFormatter = point => $"{name}: {FormatHours(point.Coordinate.PrimaryValue)} ({pct:F1}%)",
                DataLabelsPaint = null,
            });
        }

        Series = series.ToArray();
        HasData = true;
        OnPropertyChanged(nameof(Series));
    }

    private static string FormatHours(double hours)
    {
        var totalSeconds = (long)Math.Round(hours * 3600.0);
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1.0
            ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m"
            : $"{ts.Minutes}m {ts.Seconds:D2}s";
    }
}
