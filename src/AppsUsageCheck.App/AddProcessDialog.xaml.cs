using System.Windows;
using AppsUsageCheck.App.Models;
using AppsUsageCheck.App.ViewModels;

namespace AppsUsageCheck.App;

public partial class AddProcessDialog : Window
{
    public AddProcessDialog(AddProcessViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = ViewModel;
    }

    public AddProcessViewModel ViewModel { get; }

    public AddProcessRequest? SelectedRequest { get; private set; }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryCreateRequest(out var request, out var errorMessage))
        {
            MessageBox.Show(this, errorMessage, "Add Process", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedRequest = request;
        DialogResult = true;
    }
}
