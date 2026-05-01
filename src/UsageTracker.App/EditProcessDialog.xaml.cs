using System.Windows;
using UsageTracker.App.Models;
using UsageTracker.App.ViewModels;

namespace UsageTracker.App;

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
