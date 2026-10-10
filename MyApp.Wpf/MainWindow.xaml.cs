using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Wpf.Views;

namespace MyApp.Wpf;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    private void OnRegisterClick(object sender, RoutedEventArgs e) =>
        _services.GetRequiredService<RegisterWindow>().Show();

    private void OnTrackWorkoutClick(object sender, RoutedEventArgs e) =>
        _services.GetRequiredService<TrackWorkoutWindow>().Show();

    private void OnCreateTemplateClick(object sender, RoutedEventArgs e) =>
        _services.GetRequiredService<CreateTemplateWindow>().Show();
}
