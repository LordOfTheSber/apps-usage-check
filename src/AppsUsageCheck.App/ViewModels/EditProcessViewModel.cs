using System.ComponentModel;
using AppsUsageCheck.App.Models;
using AppsUsageCheck.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppsUsageCheck.App.ViewModels;

public sealed class EditProcessViewModel : ObservableObject
{
    public EditProcessViewModel(ProcessStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        RenameSection = new RenameProcessViewModel(status);
        TimeSection = new EditTimeViewModel(status);
        ProcessLabel = string.IsNullOrWhiteSpace(status.DisplayName)
            ? status.ProcessName
            : $"{status.DisplayName} ({status.ProcessName})";

        RenameSection.PropertyChanged += OnSectionPropertyChanged;
        TimeSection.PropertyChanged += OnSectionPropertyChanged;
    }

    public RenameProcessViewModel RenameSection { get; }

    public EditTimeViewModel TimeSection { get; }

    public string ProcessLabel { get; }

    public bool CanSubmit
    {
        get
        {
            var hasRenameChange = RenameSection.CanSubmit;
            var hasTimeChange = TimeSection.HasPendingChange;

            if (!hasRenameChange && !hasTimeChange)
            {
                return false;
            }

            if (hasTimeChange && TimeSection.HasValidationError)
            {
                return false;
            }

            return true;
        }
    }

    public bool TryCreateResult(out EditProcessResult? result, out string? errorMessage)
    {
        var hasRenameChange = RenameSection.CanSubmit;
        var hasTimeChange = TimeSection.HasPendingChange;

        if (!hasRenameChange && !hasTimeChange)
        {
            result = null;
            errorMessage = "Make a change before saving.";
            return false;
        }

        RenameProcessRequest? renameRequest = null;
        if (hasRenameChange)
        {
            if (!RenameSection.TryCreateRequest(out renameRequest, out errorMessage))
            {
                result = null;
                return false;
            }
        }

        EditTimeRequest? timeRequest = null;
        if (hasTimeChange)
        {
            if (!TimeSection.TryCreateRequest(out timeRequest, out errorMessage))
            {
                result = null;
                return false;
            }
        }

        result = new EditProcessResult(renameRequest, timeRequest);
        errorMessage = null;
        return true;
    }

    private void OnSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RenameProcessViewModel.CanSubmit)
            or nameof(EditTimeViewModel.HasPendingChange)
            or nameof(EditTimeViewModel.HasValidationError)
            or nameof(EditTimeViewModel.CanSubmit))
        {
            OnPropertyChanged(nameof(CanSubmit));
        }
    }
}
