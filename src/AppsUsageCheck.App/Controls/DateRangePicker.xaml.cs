using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using AppsUsageCheck.Core.Enums;
using CultureInfo = System.Globalization.CultureInfo;

namespace AppsUsageCheck.App.Controls;

public partial class DateRangePicker : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty SelectedPresetProperty =
        DependencyProperty.Register(
            nameof(SelectedPreset),
            typeof(TimeRangePreset),
            typeof(DateRangePicker),
            new FrameworkPropertyMetadata(
                TimeRangePreset.AllTime,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectionStateChanged));

    public static readonly DependencyProperty StartDateProperty =
        DependencyProperty.Register(
            nameof(StartDate),
            typeof(DateTime?),
            typeof(DateRangePicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectionStateChanged));

    public static readonly DependencyProperty EndDateProperty =
        DependencyProperty.Register(
            nameof(EndDate),
            typeof(DateTime?),
            typeof(DateRangePicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectionStateChanged));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(
            nameof(IsDropDownOpen),
            typeof(bool),
            typeof(DateRangePicker),
            new FrameworkPropertyMetadata(false, OnDropDownStateChanged));

    private static readonly IReadOnlyList<PresetOption> PresetOptionList =
    [
        new(TimeRangePreset.AllTime, "All Time"),
        new(TimeRangePreset.Today, "Today"),
        new(TimeRangePreset.Yesterday, "Yesterday"),
        new(TimeRangePreset.Last3Days, "Last 3 Days"),
        new(TimeRangePreset.Last7Days, "Last 7 Days"),
        new(TimeRangePreset.Last14Days, "Last 14 Days"),
        new(TimeRangePreset.Last30Days, "Last 30 Days"),
        new(TimeRangePreset.ThisWeek, "This Week"),
        new(TimeRangePreset.LastWeek, "Last Week"),
        new(TimeRangePreset.ThisMonth, "This Month"),
        new(TimeRangePreset.LastMonth, "Last Month"),
        new(TimeRangePreset.Custom, "Custom"),
    ];

    private bool _isSyncingCalendar;
    private DateTime? _selectionAnchorDate;

    public DateRangePicker()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<PresetOption> PresetOptions => PresetOptionList;

    public TimeRangePreset SelectedPreset
    {
        get => (TimeRangePreset)GetValue(SelectedPresetProperty);
        set => SetValue(SelectedPresetProperty, value);
    }

    public DateTime? StartDate
    {
        get => (DateTime?)GetValue(StartDateProperty);
        set => SetValue(StartDateProperty, value);
    }

    public DateTime? EndDate
    {
        get => (DateTime?)GetValue(EndDateProperty);
        set => SetValue(EndDateProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public string ButtonText => SelectedPreset == TimeRangePreset.Custom
        ? BuildCustomRangeText()
        : GetPresetLabel(SelectedPreset);

    public string SelectionSummary => StartDate.HasValue && EndDate.HasValue
        ? BuildDateRangeText(StartDate.Value, EndDate.Value)
        : "No custom dates selected yet.";

    private static void OnSelectionStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs _)
    {
        if (dependencyObject is DateRangePicker picker)
        {
            picker.HandleSelectionStateChanged();
        }
    }

    private static void OnDropDownStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not DateRangePicker picker || args.NewValue is not bool isOpen)
        {
            return;
        }

        if (isOpen)
        {
            picker.PreparePopupState();
        }
        else
        {
            picker._selectionAnchorDate = null;
        }
    }

    private void HandleSelectionStateChanged()
    {
        if (SelectedPreset != TimeRangePreset.Custom)
        {
            _selectionAnchorDate = null;
        }

        SyncCalendarSelection();
        RaiseSelectionTextChanged();
    }

    private void PreparePopupState()
    {
        SyncCalendarSelection();

        if (SelectedPreset == TimeRangePreset.Custom && StartDate.HasValue)
        {
            RangeCalendar.DisplayDate = StartDate.Value.Date;
        }
    }

    private void SyncCalendarSelection()
    {
        if (_isSyncingCalendar || !IsLoaded)
        {
            return;
        }

        _isSyncingCalendar = true;

        try
        {
            RangeCalendar.SelectedDates.Clear();

            if (SelectedPreset == TimeRangePreset.Custom && StartDate.HasValue && EndDate.HasValue)
            {
                var start = StartDate.Value.Date;
                var end = EndDate.Value.Date;
                RangeCalendar.SelectedDates.AddRange(start <= end ? start : end, start <= end ? end : start);
            }
        }
        finally
        {
            _isSyncingCalendar = false;
        }
    }

    private void PresetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
        {
            return;
        }

        if (SelectedPreset == TimeRangePreset.Custom)
        {
            if (StartDate.HasValue)
            {
                RangeCalendar.DisplayDate = StartDate.Value.Date;
            }

            return;
        }

        IsDropDownOpen = false;
    }

    private void RangeCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingCalendar || sender is not Calendar calendar)
        {
            return;
        }

        var selectedDates = calendar.SelectedDates
            .Select(date => date.Date)
            .Distinct()
            .OrderBy(date => date)
            .ToArray();

        if (selectedDates.Length == 0)
        {
            return;
        }

        if (selectedDates.Length == 1)
        {
            var clickedDate = selectedDates[0];

            if (_selectionAnchorDate.HasValue && _selectionAnchorDate.Value != clickedDate)
            {
                ApplyCalendarRange(calendar, _selectionAnchorDate.Value, clickedDate);
                return;
            }

            _selectionAnchorDate = clickedDate;
            UpdateCustomRange(clickedDate, clickedDate);
            return;
        }

        _selectionAnchorDate = null;
        UpdateCustomRange(selectedDates[0], selectedDates[^1]);
    }

    private void ApplyCalendarRange(Calendar calendar, DateTime firstDate, DateTime secondDate)
    {
        var start = firstDate <= secondDate ? firstDate : secondDate;
        var end = firstDate <= secondDate ? secondDate : firstDate;

        _isSyncingCalendar = true;

        try
        {
            calendar.SelectedDates.Clear();
            calendar.SelectedDates.AddRange(start, end);
        }
        finally
        {
            _isSyncingCalendar = false;
        }

        _selectionAnchorDate = null;
        UpdateCustomRange(start, end);
    }

    private void UpdateCustomRange(DateTime startDate, DateTime endDate)
    {
        SelectedPreset = TimeRangePreset.Custom;
        StartDate = startDate.Date;
        EndDate = endDate.Date;
    }

    private string BuildCustomRangeText()
    {
        if (StartDate.HasValue && EndDate.HasValue)
        {
            return SelectionSummary;
        }

        return "Custom Range";
    }

    private string GetPresetLabel(TimeRangePreset preset)
    {
        return PresetOptionList.FirstOrDefault(option => option.Value == preset)?.Label ?? preset.ToString();
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture);
    }

    private static string BuildDateRangeText(DateTime startDate, DateTime endDate)
    {
        return startDate.Date == endDate.Date
            ? FormatDate(startDate)
            : $"{FormatDate(startDate)} - {FormatDate(endDate)}";
    }

    private void RaiseSelectionTextChanged()
    {
        OnPropertyChanged(nameof(ButtonText));
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncCalendarSelection();
        RaiseSelectionTextChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed record PresetOption(TimeRangePreset Value, string Label);
}
