using System.Windows;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class StrengthStandardsWindow : Window
{
    public StrengthStandardsWindow(StrengthStandardsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadOverviewAsync();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
