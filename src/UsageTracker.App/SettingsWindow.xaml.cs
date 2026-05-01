using System.Windows;
using UsageTracker.App.ViewModels;

namespace UsageTracker.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = ViewModel;
    }

    public SettingsViewModel ViewModel { get; }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.TrySave(out var errorMessage, out var infoMessage))
        {
            MessageBox.Show(this, errorMessage, "Settings", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!string.IsNullOrWhiteSpace(infoMessage))
        {
            MessageBox.Show(this, infoMessage, "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        DialogResult = true;
    }
}
