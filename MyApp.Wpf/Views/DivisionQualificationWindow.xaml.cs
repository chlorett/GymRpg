using System.Windows;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class DivisionQualificationWindow : Window
{
    public DivisionQualificationWindow(DivisionQualificationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
