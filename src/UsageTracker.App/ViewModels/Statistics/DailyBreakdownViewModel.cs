using System.Globalization;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using UsageTracker.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UsageTracker.App.ViewModels.Statistics;

public partial class DailyBreakdownViewModel : ObservableObject
{
    private static readonly SKColor AxisLabelColor = new(0x5B, 0x65, 0x73);

    [ObservableProperty]
    private bool hasData;

    [ObservableProperty]
    private string emptyMessage = "Select a non-'All time' range to see daily breakdown.";

    public ISeries[] Series { get; private set; } = Array.Empty<ISeries>();

    public ICartesianAxis[] XAxes { get; private set; } = BuildXAxes(Array.Empty<string>());

    public ICartesianAxis[] YAxes { get; private set; } = BuildYAxes();

    public void Clear(string emptyMessage)
    {
        Series = Array.Empty<ISeries>();
        XAxes = BuildXAxes(Array.Empty<string>());
        EmptyMessage = emptyMessage;
        HasData = false;
        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(XAxes));
    }

    public void Update(
        IReadOnlyList<DailyUsageBucket> buckets,
        IReadOnlyDictionary<Guid, string> displayNamesByProcess)
    {
        if (buckets.Count == 0)
        {
            Clear("No usage recorded in this range.");
            return;
        }

        var allDays = buckets.Select(b => b.Day).Distinct().OrderBy(d => d).ToArray();
        var dayIndex = allDays.Select((day, i) => (day, i)).ToDictionary(t => t.day, t => t.i);

        var byProcess = buckets
            .GroupBy(b => b.TrackedProcessId)
            .OrderByDescending(g => g.Sum(x => x.RunningSeconds))
            .ToArray();

        var series = new List<ISeries>(byProcess.Length);
        var colorIndex = 0;
        foreach (var group in byProcess)
        {
            var totals = new double[allDays.Length];
            foreach (var bucket in group)
            {
                totals[dayIndex[bucket.Day]] += bucket.RunningSeconds / 3600.0;
            }

            if (totals.All(v => v <= 0))
            {
                continue;
            }

            var values = new double?[allDays.Length];
            for (var i = 0; i < totals.Length; i++)
            {
                values[i] = totals[i] > 0 ? totals[i] : null;
            }

            var name = displayNamesByProcess.TryGetValue(group.Key, out var label) ? label : "—";
            var color = StatisticsPalette.ForIndex(colorIndex++);

            series.Add(new StackedColumnSeries<double?>
            {
                Values = values,
                Name = name,
                Fill = new SolidColorPaint(color),
                Stroke = null,
                MaxBarWidth = 32,
                Padding = 4,
                YToolTipLabelFormatter = point => $"{name}: {FormatHours(point.Coordinate.PrimaryValue)}",
            });
        }

        var dayLabels = allDays.Select(d => d.ToString("MMM d", CultureInfo.CurrentCulture)).ToArray();

        Series = series.ToArray();
        XAxes = BuildXAxes(dayLabels);
        HasData = true;
        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(XAxes));
    }

    private static ICartesianAxis[] BuildXAxes(IReadOnlyList<string> labels)
    {
        return new ICartesianAxis[]
        {
            new Axis
            {
                Labels = labels.ToArray(),
                LabelsPaint = new SolidColorPaint(AxisLabelColor),
                TextSize = 11,
                LabelsRotation = labels.Count > 10 ? 30 : 0,
            },
        };
    }

    private static ICartesianAxis[] BuildYAxes()
    {
        return new ICartesianAxis[]
        {
            new Axis
            {
                Name = "Hours",
                NameTextSize = 12,
                NamePaint = new SolidColorPaint(AxisLabelColor),
                LabelsPaint = new SolidColorPaint(AxisLabelColor),
                TextSize = 11,
                MinLimit = 0,
            },
        };
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
