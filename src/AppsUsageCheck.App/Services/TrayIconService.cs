using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

namespace AppsUsageCheck.App.Services;

public sealed class TrayIconService : ITrayIconService, IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly ViewModels.MainViewModel _mainViewModel;
    private TaskbarIcon? _taskbarIcon;
    private bool _isInitialized;

    public TrayIconService(MainWindow mainWindow, ViewModels.MainViewModel mainViewModel)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
    }

    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _taskbarIcon = new TaskbarIcon
        {
            Icon = LoadIcon(),
            ToolTipText = _mainViewModel.TrayToolTipText,
            ContextMenu = CreateContextMenu(),
        };
        _taskbarIcon.TrayMouseDoubleClick += OnTrayMouseDoubleClick;

        _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
        UpdateMenuState();

        _isInitialized = true;
    }

    public void Dispose()
    {
        if (!_isInitialized)
        {
            return;
        }

        _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;

        if (_taskbarIcon is not null)
        {
            _taskbarIcon.TrayMouseDoubleClick -= OnTrayMouseDoubleClick;
            _taskbarIcon.Dispose();
            _taskbarIcon = null;
        }

        _isInitialized = false;
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("Open", OnOpenClicked));
        menu.Items.Add(CreateMenuItem("Pause All", OnPauseAllClicked));
        menu.Items.Add(CreateMenuItem("Resume All", OnResumeAllClicked));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Exit", OnExitClicked));
        return menu;
    }

    private MenuItem CreateMenuItem(string header, RoutedEventHandler onClick)
    {
        var menuItem = new MenuItem
        {
            Header = header,
        };
        menuItem.Click += onClick;
        return menuItem;
    }

    private void UpdateToolTip()
    {
        if (_taskbarIcon is not null)
        {
            _taskbarIcon.ToolTipText = _mainViewModel.TrayToolTipText;
        }
    }

    private void UpdateMenuState()
    {
        if (_taskbarIcon?.ContextMenu is null)
        {
            return;
        }

        foreach (var item in _taskbarIcon.ContextMenu.Items.OfType<MenuItem>())
        {
            switch (item.Header)
            {
                case "Pause All":
                    item.IsEnabled = _mainViewModel.PauseAllCommand.CanExecute(null);
                    break;
                case "Resume All":
                    item.IsEnabled = _mainViewModel.ResumeAllCommand.CanExecute(null);
                    break;
            }
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewModels.MainViewModel.TrayToolTipText))
        {
            UpdateToolTip();
            return;
        }

        if (e.PropertyName is nameof(ViewModels.MainViewModel.TrackedProcessCount)
            or nameof(ViewModels.MainViewModel.ActiveProcessCount))
        {
            UpdateMenuState();
        }
    }

    private void OnTrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        _mainWindow.ShowFromTray();
    }

    private void OnOpenClicked(object sender, RoutedEventArgs e)
    {
        _mainWindow.ShowFromTray();
    }

    private async void OnPauseAllClicked(object sender, RoutedEventArgs e)
    {
        await _mainViewModel.PauseAllCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    private async void OnResumeAllClicked(object sender, RoutedEventArgs e)
    {
        await _mainViewModel.ResumeAllCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        _mainViewModel.ExitCommand.Execute(null);
    }

    private static Icon LoadIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "tray-icon.ico");
        if (!File.Exists(iconPath))
        {
            return SystemIcons.Application;
        }

        using var icon = new Icon(iconPath);
        return (Icon)icon.Clone();
    }
}
