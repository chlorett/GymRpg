using System.Windows;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class RegisterWindow : Window
{
    private readonly RegisterViewModel _viewModel;

    public RegisterWindow(RegisterViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.RegistrationSucceeded += OnRegistrationSucceeded;
    }

    // PasswordBox не підтримує біндинг, тому переносимо значення у ViewModel вручну.
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordInput.Password;
    }

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.ConfirmPassword = ConfirmPasswordInput.Password;
    }

    private void OnRegistrationSucceeded(object? sender, EventArgs e)
    {
        MessageBox.Show(this, "Реєстрацію завершено успішно.", "PulseForge", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
}
