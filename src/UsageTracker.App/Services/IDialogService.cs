using UsageTracker.App.Models;
using UsageTracker.Core.Models;

namespace UsageTracker.App.Services;

public interface IDialogService
{
    Task<AddProcessRequest?> ShowAddProcessDialogAsync(
        IReadOnlyCollection<string> trackedProcessNames,
        CancellationToken cancellationToken = default);

    Task<EditProcessResult?> ShowEditProcessDialogAsync(
        ProcessStatus status,
        CancellationToken cancellationToken = default);

    void ShowSettingsDialog();

    bool Confirm(string title, string message);

    void ShowInformation(string title, string message);

    void ShowError(string title, string message);
}
