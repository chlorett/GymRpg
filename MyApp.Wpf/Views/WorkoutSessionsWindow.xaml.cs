using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class WorkoutSessionsWindow : Window
{
    private readonly WorkoutSessionsViewModel _viewModel;
    private readonly IServiceProvider _services;

    public WorkoutSessionsWindow(WorkoutSessionsViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _services = services;
        DataContext = viewModel;
        _viewModel.ExecuteSessionRequested += OnExecuteSessionRequested;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private async void OnExecuteSessionRequested(int sessionId)
    {
        var executionViewModel = _services.GetRequiredService<SessionExecutionViewModel>();
        executionViewModel.SessionId = sessionId;

        var window = new SessionExecutionWindow(executionViewModel) { Owner = this };
        window.ShowDialog();

        await _viewModel.RefreshSessionsAsync();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}