using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Enums;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _users;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<LoginViewModel> _logger;

    private string _email = "athlete@gym.com";
    private string _password = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;

    public LoginViewModel(
        IAuthService authService,
        IUserRepository users,
        CurrentUserSession userSession,
        ILogger<LoginViewModel> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy);
    }

    public event EventHandler? LoginSucceeded;

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
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
                LoginCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand LoginCommand { get; }

    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var request = new LoginRequest(Email, Password);
            var result = await _authService.LoginAsync(request);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Message ?? "Не вдалося увійти в систему.";
                _logger.LogWarning("Вхід у систему відхилено: {ErrorCode}", result.ErrorCode);
                return;
            }

            var user = await _users.GetByEmailAsync(Email.Trim());
            if (user is not null)
            {
                if (user.Role == UserRole.Trainer)
                {
                    _userSession.TrainerId = (int)user.Id;
                    _userSession.TrainerName = user.Username;
                }

                _userSession.SetUser((int)user.Id, user.Username);
                _logger.LogInformation("Користувач увійшов у систему з форми WPF: {Username}", user.Username);
            }

            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка під час входу в систему");
            ErrorMessage = "Не вдалося увійти в систему. Спробуйте пізніше.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
