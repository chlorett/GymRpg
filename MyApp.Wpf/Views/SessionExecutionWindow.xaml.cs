using System.Windows;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class SessionExecutionWindow : Window
{
    private readonly SessionExecutionViewModel _viewModel;

    public SessionExecutionWindow(SessionExecutionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}