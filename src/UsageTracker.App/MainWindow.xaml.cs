using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UsageTracker.App.ViewModels;

namespace UsageTracker.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isExitRequested;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        UpdateColumnSortDirections(_viewModel.CurrentSortColumn, _viewModel.CurrentSortDirection);

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

    private void OnProcessesGridSorting(object sender, DataGridSortingEventArgs e)
    {
        if (!Enum.TryParse<ProcessGridSortColumn>(e.Column.SortMemberPath, ignoreCase: false, out var sortColumn))
        {
            return;
        }

        e.Handled = true;
        var sortDirection = _viewModel.ApplySort(sortColumn);
        UpdateColumnSortDirections(sortColumn, sortDirection);
    }

    private void OnRowPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && !row.IsSelected)
        {
            row.IsSelected = true;
        }
    }

    private void UpdateColumnSortDirections(ProcessGridSortColumn activeColumn, ListSortDirection direction)
    {
        ProcessColumn.SortDirection = activeColumn == ProcessGridSortColumn.Process ? direction : null;
        StateColumn.SortDirection = activeColumn == ProcessGridSortColumn.State ? direction : null;
        RunningTimeColumn.SortDirection = activeColumn == ProcessGridSortColumn.RunningTime ? direction : null;
        ForegroundTimeColumn.SortDirection = activeColumn == ProcessGridSortColumn.ForegroundTime ? direction : null;
    }
}
