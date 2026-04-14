using AppsUsageCheck.App.Models;

namespace AppsUsageCheck.App.Services;

public interface IDialogService
{
    Task<AddProcessRequest?> ShowAddProcessDialogAsync(
        IReadOnlyCollection<string> trackedProcessNames,
        CancellationToken cancellationToken = default);

    bool Confirm(string title, string message);

    void ShowInformation(string title, string message);

    void ShowError(string title, string message);
}
