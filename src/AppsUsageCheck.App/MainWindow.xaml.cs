using System.ComponentModel;
using System.Windows;
using AppsUsageCheck.App.ViewModels;

namespace AppsUsageCheck.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isExitRequested;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;

        Closing += OnClosing;
        Closed += OnClosed;
        _viewModel.ExitRequested += OnExitRequested;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.ExitRequested -= OnExitRequested;
        _viewModel.Dispose();
    }

    public void ShowFromTray()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _isExitRequested = true;
        Application.Current.Shutdown();
    }
}
