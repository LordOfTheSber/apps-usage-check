using System.Collections.ObjectModel;
using AppsUsageCheck.App.Models;
using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppsUsageCheck.App.ViewModels;

public partial class AddProcessViewModel : ObservableObject
{
    private readonly IReadOnlyList<string> _availableProcesses;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyPropertyChangedFor(nameof(ResolvedProcessName))]
    private string searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyPropertyChangedFor(nameof(ResolvedProcessName))]
    private string? selectedProcessName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyPropertyChangedFor(nameof(ResolvedProcessName))]
    private string manualProcessName = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;

    public AddProcessViewModel(IProcessDetector processDetector, IReadOnlyCollection<string> trackedProcessNames)
    {
        ArgumentNullException.ThrowIfNull(processDetector);
        ArgumentNullException.ThrowIfNull(trackedProcessNames);

        var trackedLookup = new HashSet<string>(trackedProcessNames, StringComparer.Ordinal);
        _availableProcesses = processDetector.GetRunningProcessNames()
            .Where(processName => !trackedLookup.Contains(processName))
            .OrderBy(processName => processName, StringComparer.Ordinal)
            .ToArray();

        FilteredProcesses = [];
        RefreshFilter();
    }

    public ObservableCollection<string> FilteredProcesses { get; }

    public bool CanSubmit => ResolveNormalizedProcessName().Length > 0;

    public string ResolvedProcessName => ResolveNormalizedProcessName();

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilter();
    }

    partial void OnSelectedProcessNameChanged(string? value)
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(ResolvedProcessName));
    }

    partial void OnManualProcessNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(ResolvedProcessName));
    }

    public bool TryCreateRequest(out AddProcessRequest? request, out string? errorMessage)
    {
        var normalizedProcessName = ResolveNormalizedProcessName();
        if (normalizedProcessName.Length == 0)
        {
            request = null;
            errorMessage = "Enter a process name or select one from the running processes list.";
            return false;
        }

        request = new AddProcessRequest(
            normalizedProcessName,
            string.IsNullOrWhiteSpace(DisplayName) ? null : DisplayName.Trim());
        errorMessage = null;
        return true;
    }

    private void RefreshFilter()
    {
        var search = SearchText.Trim();
        var filtered = _availableProcesses
            .Where(processName => search.Length == 0 || processName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        FilteredProcesses.Clear();
        foreach (var processName in filtered)
        {
            FilteredProcesses.Add(processName);
        }

        if (SelectedProcessName is not null && !filtered.Contains(SelectedProcessName, StringComparer.Ordinal))
        {
            SelectedProcessName = null;
        }
    }

    private string ResolveNormalizedProcessName()
    {
        var candidate = string.IsNullOrWhiteSpace(ManualProcessName) ? SelectedProcessName : ManualProcessName;
        return ProcessNameNormalizer.Normalize(candidate);
    }
}
