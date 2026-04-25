using AppsUsageCheck.App.Models;
using AppsUsageCheck.Core.Models;

namespace AppsUsageCheck.App.Services;

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
