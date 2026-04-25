using System.Windows;
using AppsUsageCheck.App.Models;
using AppsUsageCheck.App.ViewModels;

namespace AppsUsageCheck.App;

public partial class RenameProcessDialog : Window
{
    public RenameProcessDialog(RenameProcessViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = ViewModel;
    }

    public RenameProcessViewModel ViewModel { get; }

    public RenameProcessRequest? SelectedRequest { get; private set; }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryCreateRequest(out var request, out var errorMessage))
        {
            MessageBox.Show(this, errorMessage, "Rename Process", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedRequest = request;
        DialogResult = true;
    }
}
