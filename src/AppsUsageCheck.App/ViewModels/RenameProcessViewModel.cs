using AppsUsageCheck.App.Models;
using AppsUsageCheck.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppsUsageCheck.App.ViewModels;

public sealed partial class RenameProcessViewModel : ObservableObject
{
    private readonly string? _originalDisplayName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private string displayName;

    public RenameProcessViewModel(ProcessStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        ProcessName = status.ProcessName;
        _originalDisplayName = NormalizeDisplayName(status.DisplayName);
        displayName = _originalDisplayName ?? string.Empty;
    }

    public string ProcessName { get; }

    public string? OriginalDisplayName => _originalDisplayName;

    public bool CanSubmit => !string.Equals(_originalDisplayName, NormalizeDisplayName(DisplayName), StringComparison.Ordinal);

    public bool TryCreateRequest(out RenameProcessRequest? request, out string? errorMessage)
    {
        var normalizedDisplayName = NormalizeDisplayName(DisplayName);
        if (string.Equals(_originalDisplayName, normalizedDisplayName, StringComparison.Ordinal))
        {
            request = null;
            errorMessage = "Make a change before saving.";
            return false;
        }

        request = new RenameProcessRequest(normalizedDisplayName);
        errorMessage = null;
        return true;
    }

    private static string? NormalizeDisplayName(string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
    }
}
