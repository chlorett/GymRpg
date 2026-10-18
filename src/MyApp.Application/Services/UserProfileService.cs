using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Rules;

namespace MyApp.Application.Services;

public sealed class UserProfileService : IUserProfileService
{
    private readonly IUserRepository _users;
    private readonly TimeProvider _time;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
            IUserRepository users,
            TimeProvider time,
            ILogger<UserProfileService> logger)
    {
        _users = users;
        _time = time;
        _logger = logger;
    }

    public async Task<UserProfileDto?> GetAsync(int userId, CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            return null;
        }

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return null;
        }

        DateOnly? birthDate = user.BirthDate is { } date ? DateOnly.FromDateTime(date) : null;

        return new UserProfileDto((int)user.Id, user.WeightKg, user.HeightCm, birthDate);
    }

    public async Task<OperationResult> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            _logger.LogWarning("Profile update rejected: request is null");
            return Fail(ErrorCodes.ValidationFailed, nameof(request), "Запит не може бути порожнім.");
        }

        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Reject(invalid, request);
        }

        var updated = await _users.UpdateProfileAsync(
            request.UserId,
            request.WeightKg,
            request.HeightCm,
            request.BirthDate,
            ct);

        if (!updated)
        {
            var notFound = Fail(ErrorCodes.UserNotFound, nameof(UpdateProfileRequest.UserId), "Користувача не знайдено.");
            return Reject(notFound, request);
        }

        _logger.LogInformation("Profile updated for {UserId}", request.UserId);

        return OperationResult.Success();
    }

    private static OperationResult Fail(string errorCode, string field, string message)
    {
        return OperationResult.Fail(errorCode, field, message);
    }

    private OperationResult? Validate(UpdateProfileRequest request)
    {
        if (request.UserId <= 0)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(UpdateProfileRequest.UserId), "Некоректний користувач.");
        }

        if (request.WeightKg is null && request.HeightCm is null && request.BirthDate is null)
        {
            return Fail(ErrorCodes.ValidationFailed, "Profile", "Вкажіть хоча б одне значення профілю.");
        }

        if (request.WeightKg is < ProfileRules.MinWeightKg or > ProfileRules.MaxWeightKg)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(UpdateProfileRequest.WeightKg),
                $"Вага має бути від {ProfileRules.MinWeightKg} до {ProfileRules.MaxWeightKg} кг.");
        }

        if (request.HeightCm is < ProfileRules.MinHeightCm or > ProfileRules.MaxHeightCm)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(UpdateProfileRequest.HeightCm),
                $"Зріст має бути від {ProfileRules.MinHeightCm} до {ProfileRules.MaxHeightCm} см.");
        }

        if (request.BirthDate is { } birthDate)
        {
            return ValidateBirthDate(birthDate);
        }

        return null;
    }

    private OperationResult? ValidateBirthDate(DateOnly birthDate)
    {
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (birthDate > today)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(UpdateProfileRequest.BirthDate), "Дата народження не може бути в майбутньому.");
        }

        var age = ProfileRules.CalculateAge(birthDate, today);
        if (age is < ProfileRules.MinAge or > ProfileRules.MaxAge)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(UpdateProfileRequest.BirthDate),
                $"Вік має бути від {ProfileRules.MinAge} до {ProfileRules.MaxAge} років.");
        }

        return null;
    }

    private OperationResult Reject(OperationResult result, UpdateProfileRequest request)
    {
        _logger.LogWarning(
            "Profile update rejected: user {UserId}, error {ErrorCode}, field {Field}",
            request.UserId,
            result.ErrorCode,
            result.Field);

        return result;
    }
}