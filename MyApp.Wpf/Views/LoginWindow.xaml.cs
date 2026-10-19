using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;
    private readonly IServiceProvider _services;

    public LoginWindow(LoginViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _services = services;
        DataContext = viewModel;
        viewModel.LoginSucceeded += OnLoginSucceeded;
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordInput.Password;
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        MessageBox.Show(this, "Вхід виконано успішно!", "PulseForge", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void OnOpenRegisterClick(object sender, RoutedEventArgs e)
    {
        _services.GetRequiredService<RegisterWindow>().Show();
        Close();
    }
}
