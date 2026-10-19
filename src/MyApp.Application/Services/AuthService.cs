using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Rules;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _time;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IPasswordHasher hasher,
        TimeProvider time,
        ILogger<AuthService> logger)
    {
        _users = users;
        _hasher = hasher;
        _time = time;
        _logger = logger;
    }

    public async Task<OperationResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(request), "Запит не може бути порожнім.");
        }

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var username = (request.Username ?? string.Empty).Trim();
        var now = _time.GetUtcNow();

        var invalid = Validate(request, email, username, DateOnly.FromDateTime(now.UtcDateTime));
        if (invalid is not null)
        {
            return Reject(invalid);
        }

        if (await _users.GetByEmailAsync(email, ct) is not null)
        {
            var taken = Fail(ErrorCodes.EmailTaken, nameof(RegisterRequest.Email), "Користувач з таким email уже існує.");
            return Reject(taken);
        }

        if (await _users.GetByUsernameAsync(username, ct) is not null)
        {
            var taken = Fail(ErrorCodes.UsernameTaken, nameof(RegisterRequest.Username), "Це ім'я користувача вже зайняте.");
            return Reject(taken);
        }

        await _users.AddAsync(CreateUser(request, email, username, now), ct);

        _logger.LogInformation("User registered: {Username}", username);

        return OperationResult.Success();
    }

    public async Task<OperationResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            _logger.LogWarning("Login rejected: request is null");
            return Fail(ErrorCodes.ValidationFailed, nameof(request), "Запит не може бути порожнім.");
        }

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsValidEmail(email))
        {
            var invalidEmail = Fail(ErrorCodes.ValidationFailed, nameof(LoginRequest.Email), "Вкажіть коректний email.");
            return RejectLogin(invalidEmail, email);
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            var emptyPass = Fail(ErrorCodes.ValidationFailed, nameof(LoginRequest.Password), "Введіть пароль.");
            return RejectLogin(emptyPass, email);
        }

        var user = await _users.GetByEmailAsync(email, ct);
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            var invalidCreds = Fail(ErrorCodes.InvalidCredentials, nameof(LoginRequest.Email), "Невірний email або пароль.");
            return RejectLogin(invalidCreds, email);
        }

        _logger.LogInformation("User logged in: {Username}", user.Username);

        return OperationResult.Success();
    }

    private static OperationResult? Validate(RegisterRequest request, string email, string username, DateOnly today)
    {
        return ValidateUsername(username)
            ?? ValidateEmail(email)
            ?? ValidatePassword(request.Password, request.ConfirmPassword)
            ?? ValidateWeight(request.WeightKg)
            ?? ValidateHeight(request.HeightCm)
            ?? ValidateBirthDate(request.BirthDate, today);
    }

    private static OperationResult? ValidateUsername(string username)
    {
        if (username.Length is < AuthRules.MinUsernameLength or > AuthRules.MaxUsernameLength)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(RegisterRequest.Username),
                $"Ім'я користувача має містити від {AuthRules.MinUsernameLength} до {AuthRules.MaxUsernameLength} символів.");
        }

        if (!username.All(IsUsernameChar))
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(RegisterRequest.Username),
                "Ім'я користувача може містити лише латинські літери, цифри та підкреслення.");
        }

        return null;
    }

    private static bool IsUsernameChar(char symbol)
    {
        return char.IsAsciiLetterOrDigit(symbol) || symbol == '_';
    }

    private static OperationResult? ValidateEmail(string email)
    {
        if (!IsValidEmail(email))
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(RegisterRequest.Email), "Вкажіть коректний email.");
        }

        return null;
    }

    private static bool IsValidEmail(string email)
    {
        if (email.Length is 0 or > AuthRules.MaxEmailLength)
        {
            return false;
        }

        if (!MailAddress.TryCreate(email, out var address))
        {
            return false;
        }

        // Відсікаємо форми на кшталт "Ім'я <a@b.com>" і домени без крапки (a@b).
        return string.Equals(address.Address, email, StringComparison.Ordinal)
            && address.Host.Contains('.', StringComparison.Ordinal);
    }

    private static OperationResult? ValidatePassword(string password, string confirmPassword)
    {
        if (string.IsNullOrEmpty(password) || password.Length < AuthRules.MinPasswordLength)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(RegisterRequest.Password),
                $"Пароль має містити щонайменше {AuthRules.MinPasswordLength} символів.");
        }

        if (Encoding.UTF8.GetByteCount(password) > AuthRules.MaxPasswordBytes)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(RegisterRequest.Password),
                $"Пароль занадто довгий: не більше {AuthRules.MaxPasswordBytes} байтів (кирилична літера займає 2 байти).");
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(RegisterRequest.ConfirmPassword), "Паролі не збігаються.");
        }

        return null;
    }

    private static OperationResult? ValidateWeight(decimal weightKg)
    {
        if (weightKg is < AuthRules.MinWeightKg or > AuthRules.MaxWeightKg)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(RegisterRequest.WeightKg),
                $"Вага має бути від {AuthRules.MinWeightKg} до {AuthRules.MaxWeightKg} кг.");
        }

        return null;
    }

    private static OperationResult? ValidateHeight(decimal heightCm)
    {
        if (heightCm is < AuthRules.MinHeightCm or > AuthRules.MaxHeightCm)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(RegisterRequest.HeightCm),
                $"Зріст має бути від {AuthRules.MinHeightCm} до {AuthRules.MaxHeightCm} см.");
        }

        return null;
    }

    private static OperationResult? ValidateBirthDate(DateOnly birthDate, DateOnly today)
    {
        if (birthDate > today)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(RegisterRequest.BirthDate), "Дата народження не може бути в майбутньому.");
        }

        var age = CalculateAge(birthDate, today);

        if (age < AuthRules.MinAge)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(RegisterRequest.BirthDate),
                $"Реєстрація доступна з {AuthRules.MinAge} років.");
        }

        if (age > AuthRules.MaxAge)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(RegisterRequest.BirthDate), "Вкажіть коректну дату народження.");
        }

        return null;
    }

    private static int CalculateAge(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;

        // День народження цього року ще не настав.
        if (birthDate.AddYears(age) > today)
        {
            age--;
        }

        return age;
    }

    private static OperationResult Fail(string errorCode, string field, string message)
    {
        return OperationResult.Fail(errorCode, field, message);
    }

    private User CreateUser(RegisterRequest request, string email, string username, DateTimeOffset now)
    {
        return new User
        {
            Email = email,
            Username = username,
            PasswordHash = _hasher.Hash(request.Password),
            Role = UserRole.User,
            WeightKg = request.WeightKg,
            HeightCm = request.HeightCm,
            BirthDate = request.BirthDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private OperationResult Reject(OperationResult result)
    {
        _logger.LogWarning(
            "Registration rejected: error {ErrorCode}, field {Field}",
            result.ErrorCode,
            result.Field);

        return result;
    }

    private OperationResult RejectLogin(OperationResult result, string email)
    {
        _logger.LogWarning(
            "Login rejected for {Email}: error {ErrorCode}, field {Field}",
            email,
            result.ErrorCode,
            result.Field);

        return result;
    }
}
