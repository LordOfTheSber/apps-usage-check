using System.Windows;
using AppsUsageCheck.App.ViewModels;

namespace AppsUsageCheck.App;

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
        if (!ViewModel.TrySave(out var errorMessage))
        {
            MessageBox.Show(this, errorMessage, "Settings", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
    }
}
