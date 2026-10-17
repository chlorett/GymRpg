using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _users;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<RegisterViewModel> _logger;

    private string _email = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _weightKg = string.Empty;
    private string _heightCm = string.Empty;
    private DateTime? _birthDate;
    private string? _errorMessage;
    private bool _isBusy;

    public RegisterViewModel(
        IAuthService authService,
        IUserRepository users,
        CurrentUserSession userSession,
        ILogger<RegisterViewModel> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RegisterCommand = new AsyncRelayCommand(RegisterAsync, () => !IsBusy);
    }

    public event EventHandler? RegistrationSucceeded;

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    // PasswordBox не підтримує біндинг, тому значення передає code-behind вікна.
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    // Вага і зріст — текст, щоб приймати і "75.5", і "75,5" незалежно від мови Windows.
    public string WeightKg
    {
        get => _weightKg;
        set => SetProperty(ref _weightKg, value);
    }

    public string HeightCm
    {
        get => _heightCm;
        set => SetProperty(ref _heightCm, value);
    }

    public DateTime? BirthDate
    {
        get => _birthDate;
        set => SetProperty(ref _birthDate, value);
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
                RegisterCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand RegisterCommand { get; }

    private static bool TryParseNumber(string text, out decimal value)
    {
        var normalized = (text ?? string.Empty).Trim().Replace(',', '.');

        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private RegisterRequest? BuildRequest()
    {
        if (!TryParseNumber(WeightKg, out var weight))
        {
            ErrorMessage = "Вкажіть вагу числом, наприклад 75.5.";
            return null;
        }

        if (!TryParseNumber(HeightCm, out var height))
        {
            ErrorMessage = "Вкажіть зріст числом, наприклад 180.";
            return null;
        }

        if (BirthDate is null)
        {
            ErrorMessage = "Оберіть дату народження.";
            return null;
        }

        return new RegisterRequest(
            Email,
            Username,
            Password,
            ConfirmPassword,
            weight,
            height,
            DateOnly.FromDateTime(BirthDate.Value));
    }

    private async Task RegisterAsync()
    {
        ErrorMessage = null;

        var request = BuildRequest();
        if (request is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.RegisterAsync(request);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Message ?? "Не вдалося зареєструватися.";
                _logger.LogWarning("Реєстрацію відхилено: {ErrorCode}", result.ErrorCode);
                return;
            }

            var registeredUser = await _users.GetByUsernameAsync(request.Username);
            if (registeredUser is not null)
            {
                _userSession.SetUser((int)registeredUser.Id, registeredUser.Username);
            }

            _logger.LogInformation("Користувача зареєстровано з форми WPF: {Username}", request.Username);
            RegistrationSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка під час реєстрації");
            ErrorMessage = "Не вдалося зареєструватися. Спробуйте пізніше.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
