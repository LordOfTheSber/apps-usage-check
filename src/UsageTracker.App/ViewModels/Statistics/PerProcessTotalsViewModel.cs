using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UsageTracker.App.ViewModels.Statistics;

public partial class PerProcessTotalsViewModel : ObservableObject
{
    private static readonly SKColor AxisLabelColor = new(0x5B, 0x65, 0x73);

    [ObservableProperty]
    private bool isRunningSelected = true;

    [ObservableProperty]
    private bool hasData;

    public ISeries[] Series { get; private set; } = Array.Empty<ISeries>();

    public ICartesianAxis[] XAxes { get; private set; } = BuildXAxes();

    public ICartesianAxis[] YAxes { get; private set; } = BuildYAxes(Array.Empty<string>());

    private IReadOnlyDictionary<Guid, long> _runningSecondsByProcess = new Dictionary<Guid, long>();
    private IReadOnlyDictionary<Guid, long> _foregroundSecondsByProcess = new Dictionary<Guid, long>();
    private IReadOnlyDictionary<Guid, string> _displayNamesByProcess = new Dictionary<Guid, string>();

    partial void OnIsRunningSelectedChanged(bool value)
    {
        Render();
    }

    public void Update(
        IReadOnlyDictionary<Guid, long> runningSecondsByProcess,
        IReadOnlyDictionary<Guid, long> foregroundSecondsByProcess,
        IReadOnlyDictionary<Guid, string> displayNamesByProcess)
    {
        _runningSecondsByProcess = runningSecondsByProcess;
        _foregroundSecondsByProcess = foregroundSecondsByProcess;
        _displayNamesByProcess = displayNamesByProcess;
        Render();
    }

    private void Render()
    {
        var source = IsRunningSelected ? _runningSecondsByProcess : _foregroundSecondsByProcess;

        var ordered = source
            .Where(kvp => kvp.Value > 0L)
            .OrderBy(kvp => kvp.Value)
            .ToArray();

        HasData = ordered.Length > 0;

        if (ordered.Length == 0)
        {
            Series = Array.Empty<ISeries>();
            YAxes = BuildYAxes(Array.Empty<string>());
            OnPropertyChanged(nameof(Series));
            OnPropertyChanged(nameof(YAxes));
            return;
        }

        var labels = ordered
            .Select(kvp => _displayNamesByProcess.TryGetValue(kvp.Key, out var name) ? name : "—")
            .ToArray();
        var hours = ordered.Select(kvp => kvp.Value / 3600.0).ToArray();
        var color = IsRunningSelected ? new SKColor(0x1B, 0x6B, 0x7A) : new SKColor(0xC5, 0x80, 0x4D);

        Series = new ISeries[]
        {
            new RowSeries<double>
            {
                Values = hours,
                Name = IsRunningSelected ? "Running" : "Foreground",
                Fill = new SolidColorPaint(color),
                Stroke = null,
                MaxBarWidth = 28,
                Padding = 6,
                DataLabelsPaint = new SolidColorPaint(AxisLabelColor),
                DataLabelsSize = 12,
                DataLabelsFormatter = point => FormatHours(point.Coordinate.PrimaryValue),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Right,
                XToolTipLabelFormatter = point => FormatHours(point.Coordinate.PrimaryValue),
            },
        };

        YAxes = BuildYAxes(labels);

        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(YAxes));
    }

    private static ICartesianAxis[] BuildXAxes()
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

    private static ICartesianAxis[] BuildYAxes(IReadOnlyList<string> labels)
    {
        return new ICartesianAxis[]
        {
            new Axis
            {
                Labels = labels.ToArray(),
                LabelsPaint = new SolidColorPaint(AxisLabelColor),
                TextSize = 12,
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
