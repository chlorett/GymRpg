using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Wpf.Services;
using MyApp.Wpf.Views;

namespace MyApp.Wpf;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly CurrentUserSession _userSession;

    public MainWindow(IServiceProvider services, CurrentUserSession userSession)
    {
        InitializeComponent();
        _services = services;
        _userSession = userSession;
        UpdateActiveUserDisplay();
        _userSession.SessionChanged += UpdateActiveUserDisplay;
    }

    private void UpdateActiveUserDisplay()
    {
        Dispatcher.Invoke(() =>
        {
            ActiveUserText.Text = $"Поточний атлет: {_userSession.UserName} (ID: {_userSession.UserId}) | Тренер: {_userSession.TrainerName} (ID: {_userSession.TrainerId})";
        });
    }

    private void OnLoginClick(object sender, RoutedEventArgs e) =>
        _services.GetRequiredService<LoginWindow>().Show();

    private void OnRegisterClick(object sender, RoutedEventArgs e) =>
        _services.GetRequiredService<RegisterWindow>().Show();

    private void OnTrackWorkoutClick(object sender, RoutedEventArgs e) =>
        _services.GetRequiredService<TrackWorkoutWindow>().Show();

    private void OnUserProgressClick(object sender, RoutedEventArgs e) =>
        _services.GetRequiredService<UserProgressWindow>().Show();

    private void OnCreateTemplateClick(object sender, RoutedEventArgs e) =>
        _services.GetRequiredService<CreateTemplateWindow>().Show();

    private void OnProfileClick(object sender, RoutedEventArgs e) =>
        _services.GetRequiredService<ProfileWindow>().Show();
}
