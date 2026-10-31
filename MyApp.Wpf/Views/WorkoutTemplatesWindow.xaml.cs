using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class WorkoutTemplatesWindow : Window
{
    private readonly WorkoutTemplatesViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;

    public WorkoutTemplatesWindow(
        WorkoutTemplatesViewModel viewModel,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadTemplatesAsync();
    }

    private async void OnCreateNewTemplateClick(object sender, RoutedEventArgs e)
    {
        var createWindow = _serviceProvider.GetRequiredService<CreateTemplateWindow>();
        createWindow.Owner = this;
        createWindow.ShowDialog();
        await _viewModel.LoadTemplatesAsync();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
