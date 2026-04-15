using System.Windows;
using AppsUsageCheck.App.Models;
using AppsUsageCheck.App.ViewModels;

namespace AppsUsageCheck.App;

public partial class EditTimeDialog : Window
{
    public EditTimeDialog(EditTimeViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = ViewModel;
    }

    public EditTimeViewModel ViewModel { get; }

    public EditTimeRequest? SelectedRequest { get; private set; }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryCreateRequest(out var request, out var errorMessage))
        {
            MessageBox.Show(this, errorMessage, "Edit Time", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedRequest = request;
        DialogResult = true;
    }
}
