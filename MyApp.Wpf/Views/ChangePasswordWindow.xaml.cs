using System.Windows;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class ChangePasswordWindow : Window
{
    private readonly ChangePasswordViewModel _viewModel;

    public ChangePasswordWindow(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PasswordChangeSucceeded += OnPasswordChangeSucceeded;
    }

    // PasswordBox не підтримує біндинг, тому переносимо значення у ViewModel вручну.
    private void OnCurrentPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentPassword = CurrentPasswordInput.Password;
    }

    private void OnNewPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.NewPassword = NewPasswordInput.Password;
    }

    private void OnConfirmNewPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.ConfirmNewPassword = ConfirmNewPasswordInput.Password;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPasswordChangeSucceeded(object? sender, EventArgs e)
    {
        MessageBox.Show(this, "Пароль успішно змінено.", "PulseForge", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
}
