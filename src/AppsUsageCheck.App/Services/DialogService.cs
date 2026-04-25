using System.Windows;
using AppsUsageCheck.App.Models;
using AppsUsageCheck.App.ViewModels;
using AppsUsageCheck.Core.Interfaces;
using AppsUsageCheck.Core.Models;

namespace AppsUsageCheck.App.Services;

public sealed class DialogService : IDialogService
{
    private readonly IProcessDetector _processDetector;
    private readonly IAutoStartService _autoStartService;
    private readonly IAppSettingsStore _appSettingsStore;

    public DialogService(
        IProcessDetector processDetector,
        IAutoStartService autoStartService,
        IAppSettingsStore appSettingsStore)
    {
        _processDetector = processDetector ?? throw new ArgumentNullException(nameof(processDetector));
        _autoStartService = autoStartService ?? throw new ArgumentNullException(nameof(autoStartService));
        _appSettingsStore = appSettingsStore ?? throw new ArgumentNullException(nameof(appSettingsStore));
    }

    public Task<AddProcessRequest?> ShowAddProcessDialogAsync(
        IReadOnlyCollection<string> trackedProcessNames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var viewModel = new AddProcessViewModel(_processDetector, trackedProcessNames);
        var dialog = new AddProcessDialog(viewModel)
        {
            Owner = GetOwnerWindow(),
        };

        var result = dialog.ShowDialog() == true ? dialog.SelectedRequest : null;
        return Task.FromResult(result);
    }

    public Task<RenameProcessRequest?> ShowRenameProcessDialogAsync(
        ProcessStatus status,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(status);

        var viewModel = new RenameProcessViewModel(status);
        var dialog = new RenameProcessDialog(viewModel)
        {
            Owner = GetOwnerWindow(),
        };

        var result = dialog.ShowDialog() == true ? dialog.SelectedRequest : null;
        return Task.FromResult(result);
    }

    public Task<EditTimeRequest?> ShowEditTimeDialogAsync(
        ProcessStatus status,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(status);

        var viewModel = new EditTimeViewModel(status);
        var dialog = new EditTimeDialog(viewModel)
        {
            Owner = GetOwnerWindow(),
        };

        var result = dialog.ShowDialog() == true ? dialog.SelectedRequest : null;
        return Task.FromResult(result);
    }

    public void ShowSettingsDialog()
    {
        var viewModel = new SettingsViewModel(_autoStartService, _appSettingsStore);
        var dialog = new SettingsWindow(viewModel)
        {
            Owner = GetOwnerWindow(),
        };

        dialog.ShowDialog();
    }

    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(GetOwnerWindow(), message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;
    }

    public void ShowInformation(string title, string message)
    {
        MessageBox.Show(GetOwnerWindow(), message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(GetOwnerWindow(), message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static Window? GetOwnerWindow()
    {
        return Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? Application.Current.MainWindow;
    }
}
