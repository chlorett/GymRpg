using System.Windows;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class CreateTemplateWindow : Window
{
    public CreateTemplateWindow(CreateTemplateViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadExercisesAsync();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}