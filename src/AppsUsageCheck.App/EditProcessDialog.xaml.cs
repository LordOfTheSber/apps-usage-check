using System.Windows;
using AppsUsageCheck.App.Models;
using AppsUsageCheck.App.ViewModels;

namespace AppsUsageCheck.App;

public partial class EditProcessDialog : Window
{
    public EditProcessDialog(EditProcessViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = ViewModel;
    }

    public EditProcessViewModel ViewModel { get; }

    public EditProcessResult? SelectedResult { get; private set; }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryCreateResult(out var result, out var errorMessage))
        {
            MessageBox.Show(this, errorMessage, "Edit Process", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedResult = result;
        DialogResult = true;
    }
}
