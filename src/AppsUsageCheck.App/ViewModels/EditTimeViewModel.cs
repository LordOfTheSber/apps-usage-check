using AppsUsageCheck.App.Converters;
using AppsUsageCheck.App.Models;
using AppsUsageCheck.Core.Enums;
using AppsUsageCheck.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppsUsageCheck.App.ViewModels;

public sealed class EditTimeViewModel : ObservableObject
{
    private TimeAdjustmentTargetOption _selectedTargetOption;
    private AdjustmentOperationOption _selectedOperationOption;
    private string _hoursText = "0";
    private string _minutesText = "0";
    private string _secondsText = "0";
    private string _reason = string.Empty;

    public EditTimeViewModel(ProcessStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        ProcessName = status.ProcessName;
        DisplayName = status.DisplayName;
        CurrentRunningSeconds = status.TotalRunningSeconds;
        CurrentForegroundSeconds = status.ForegroundSeconds;

        TargetOptions =
        [
            new TimeAdjustmentTargetOption(TimeAdjustmentTarget.Running, "Running time"),
            new TimeAdjustmentTargetOption(TimeAdjustmentTarget.Foreground, "Foreground time"),
        ];
        OperationOptions =
        [
            new AdjustmentOperationOption(false, "Add"),
            new AdjustmentOperationOption(true, "Subtract"),
        ];

        _selectedTargetOption = TargetOptions[0];
        _selectedOperationOption = OperationOptions[0];
    }

    public string ProcessName { get; }

    public string? DisplayName { get; }

    public string ProcessLabel => string.IsNullOrWhiteSpace(DisplayName)
        ? ProcessName
        : $"{DisplayName} ({ProcessName})";

    public long CurrentRunningSeconds { get; }

    public long CurrentForegroundSeconds { get; }

    public IReadOnlyList<TimeAdjustmentTargetOption> TargetOptions { get; }

    public IReadOnlyList<AdjustmentOperationOption> OperationOptions { get; }

    public TimeAdjustmentTargetOption SelectedTargetOption
    {
        get => _selectedTargetOption;
        set
        {
            if (value is null)
            {
                return;
            }

            if (SetProperty(ref _selectedTargetOption, value))
            {
                NotifyComputedPropertiesChanged();
            }
        }
    }

    public AdjustmentOperationOption SelectedOperationOption
    {
        get => _selectedOperationOption;
        set
        {
            if (value is null)
            {
                return;
            }

            if (SetProperty(ref _selectedOperationOption, value))
            {
                NotifyComputedPropertiesChanged();
            }
        }
    }

    public string HoursText
    {
        get => _hoursText;
        set
        {
            if (SetProperty(ref _hoursText, value))
            {
                NotifyComputedPropertiesChanged();
            }
        }
    }

    public string MinutesText
    {
        get => _minutesText;
        set
        {
            if (SetProperty(ref _minutesText, value))
            {
                NotifyComputedPropertiesChanged();
            }
        }
    }

    public string SecondsText
    {
        get => _secondsText;
        set
        {
            if (SetProperty(ref _secondsText, value))
            {
                NotifyComputedPropertiesChanged();
            }
        }
    }

    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public string SelectedTargetLabel => SelectedTargetOption.Label;

    public long CurrentSelectedTotalSeconds => SelectedTargetOption.Target == TimeAdjustmentTarget.Running
        ? CurrentRunningSeconds
        : CurrentForegroundSeconds;

    public long PreviewTotalSeconds => CurrentSelectedTotalSeconds + SignedAdjustmentSeconds;

    public bool HasValidationError => GetValidationError() is not null;

    public string ValidationMessage => GetValidationError() ?? string.Empty;

    public bool CanSubmit => GetValidationError() is null;

    public string SignedAdjustmentDisplayText
    {
        get
        {
            if (!TryGetMagnitudeSeconds(out var magnitudeSeconds, out _))
            {
                return "Enter a valid adjustment";
            }

            if (magnitudeSeconds == 0)
            {
                return "0s";
            }

            var prefix = SelectedOperationOption.IsSubtract ? "-" : "+";
            return prefix + SecondsToTimeStringConverter.FormatSeconds(magnitudeSeconds);
        }
    }

    public bool TryCreateRequest(out EditTimeRequest? request, out string? errorMessage)
    {
        errorMessage = GetValidationError();
        if (errorMessage is not null)
        {
            request = null;
            return false;
        }

        request = new EditTimeRequest(
            SelectedTargetOption.Target,
            SignedAdjustmentSeconds,
            string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim());
        return true;
    }

    private long SignedAdjustmentSeconds
    {
        get
        {
            if (!TryGetMagnitudeSeconds(out var magnitudeSeconds, out _))
            {
                return 0;
            }

            return SelectedOperationOption.IsSubtract ? -magnitudeSeconds : magnitudeSeconds;
        }
    }

    private string? GetValidationError()
    {
        if (!TryGetMagnitudeSeconds(out var magnitudeSeconds, out var parseError))
        {
            return parseError;
        }

        if (magnitudeSeconds == 0)
        {
            return "Enter a non-zero adjustment.";
        }

        var previewRunningSeconds = CurrentRunningSeconds + (SelectedTargetOption.Target == TimeAdjustmentTarget.Running ? SignedAdjustmentSeconds : 0);
        var previewForegroundSeconds = CurrentForegroundSeconds + (SelectedTargetOption.Target == TimeAdjustmentTarget.Foreground ? SignedAdjustmentSeconds : 0);

        if (previewRunningSeconds < 0)
        {
            return "Running time cannot be reduced below zero.";
        }

        if (previewForegroundSeconds < 0)
        {
            return "Foreground time cannot be reduced below zero.";
        }

        if (previewForegroundSeconds > previewRunningSeconds)
        {
            return SelectedTargetOption.Target == TimeAdjustmentTarget.Running
                ? "Running time cannot be reduced below current foreground time."
                : "Foreground time cannot exceed running time.";
        }

        return null;
    }

    private bool TryGetMagnitudeSeconds(out long totalSeconds, out string? errorMessage)
    {
        if (!TryParsePart(HoursText, 0, int.MaxValue, "Hours", out var hours, out errorMessage) ||
            !TryParsePart(MinutesText, 0, 59, "Minutes", out var minutes, out errorMessage) ||
            !TryParsePart(SecondsText, 0, 59, "Seconds", out var seconds, out errorMessage))
        {
            totalSeconds = 0;
            return false;
        }

        totalSeconds = (hours * 3600L) + (minutes * 60L) + seconds;
        errorMessage = null;
        return true;
    }

    private void NotifyComputedPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedTargetLabel));
        OnPropertyChanged(nameof(CurrentSelectedTotalSeconds));
        OnPropertyChanged(nameof(PreviewTotalSeconds));
        OnPropertyChanged(nameof(HasValidationError));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(SignedAdjustmentDisplayText));
    }

    private static bool TryParsePart(
        string text,
        int minimumValue,
        int maximumValue,
        string label,
        out int value,
        out string? errorMessage)
    {
        var candidateText = string.IsNullOrWhiteSpace(text) ? "0" : text.Trim();
        if (!int.TryParse(candidateText, out value))
        {
            errorMessage = $"{label} must be a whole number.";
            return false;
        }

        if (value < minimumValue || value > maximumValue)
        {
            errorMessage = $"{label} must be between {minimumValue} and {maximumValue}.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public sealed record TimeAdjustmentTargetOption(TimeAdjustmentTarget Target, string Label);

    public sealed record AdjustmentOperationOption(bool IsSubtract, string Label);
}
