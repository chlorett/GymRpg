using System.Windows;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class UserProgressWindow : Window
{
    public UserProgressWindow(UserProgressViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadProgressAsync();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
