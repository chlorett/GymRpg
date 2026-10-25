using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class ChangePasswordViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<ChangePasswordViewModel> _logger;

    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmNewPassword = string.Empty;
    private string? _currentPasswordError;
    private string? _newPasswordError;
    private string? _confirmNewPasswordError;
    private string? _errorMessage;
    private bool _isBusy;

    public ChangePasswordViewModel(
        IAuthService authService,
        CurrentUserSession userSession,
        ILogger<ChangePasswordViewModel> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        SaveCommand = new AsyncRelayCommand(ChangePasswordAsync, () => !IsBusy);
    }

    public event EventHandler? PasswordChangeSucceeded;

    // PasswordBox не підтримує біндинг, тому значення передає code-behind вікна.
    public string CurrentPassword
    {
        get => _currentPassword;
        set => SetProperty(ref _currentPassword, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public string ConfirmNewPassword
    {
        get => _confirmNewPassword;
        set => SetProperty(ref _confirmNewPassword, value);
    }

    public string? CurrentPasswordError
    {
        get => _currentPasswordError;
        set => SetProperty(ref _currentPasswordError, value);
    }

    public string? NewPasswordError
    {
        get => _newPasswordError;
        set => SetProperty(ref _newPasswordError, value);
    }

    public string? ConfirmNewPasswordError
    {
        get => _confirmNewPasswordError;
        set => SetProperty(ref _confirmNewPasswordError, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string UserInfo => $"Користувач: {_userSession.UserName} (ID: {_userSession.UserId})";

    public IAsyncRelayCommand SaveCommand { get; }

    private void ClearErrors()
    {
        CurrentPasswordError = null;
        NewPasswordError = null;
        ConfirmNewPasswordError = null;
        ErrorMessage = null;
    }

    private void ShowError(OperationResult result)
    {
        var message = result.Message ?? "Не вдалося змінити пароль.";

        switch (result.Field)
        {
            case nameof(ChangePasswordRequest.CurrentPassword):
                CurrentPasswordError = message;
                break;
            case nameof(ChangePasswordRequest.NewPassword):
                NewPasswordError = message;
                break;
            case nameof(ChangePasswordRequest.ConfirmNewPassword):
                ConfirmNewPasswordError = message;
                break;
            default:
                ErrorMessage = message;
                break;
        }
    }

    private async Task ChangePasswordAsync()
    {
        ClearErrors();
        IsBusy = true;

        try
        {
            var request = new ChangePasswordRequest(_userSession.UserId, CurrentPassword, NewPassword, ConfirmNewPassword);
            var result = await _authService.ChangePasswordAsync(request);

            if (!result.IsSuccess)
            {
                ShowError(result);
                _logger.LogWarning("Зміну пароля відхилено: {ErrorCode}", result.ErrorCode);
                return;
            }

            _logger.LogInformation("Користувач {UserId} змінив пароль з форми WPF", _userSession.UserId);
            PasswordChangeSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка під час зміни пароля");
            ErrorMessage = "Не вдалося змінити пароль. Спробуйте пізніше.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
