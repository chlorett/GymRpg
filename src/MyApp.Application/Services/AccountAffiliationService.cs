using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Rules;

namespace MyApp.Application.Services;

public sealed class AccountAffiliationService : IAccountAffiliationService
{
    private const string UserNotFoundMessage = "Користувача не знайдено.";

    private readonly IUserRepository _users;
    private readonly ILogger<AccountAffiliationService> _logger;

    public AccountAffiliationService(IUserRepository users, ILogger<AccountAffiliationService> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<UserAffiliationDto?> GetAffiliationDataAsync(int userId, CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            _logger.LogWarning("Affiliation load rejected: invalid userId {UserId}", userId);
            return null;
        }

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            _logger.LogWarning("Affiliation load rejected: user {UserId} not found", userId);
            return null;
        }

        var regions = (await _users.GetRegionsAsync(ct))
            .Select(r => new RegionDto(r.Id, r.Name))
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var gyms = (await _users.GetGymsAsync(ct))
            .Select(g => new GymDto(g.Id, g.RegionId, g.Name))
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var divisions = (await _users.GetDivisionsAsync(ct))
            .Select(d => new DivisionDto(d.Id, d.Name))
            .ToList();

        var regionId = gyms.Find(g => g.Id == user.GymId)?.RegionId;
        var membersCount = user.GymId is { } gymId
            ? await _users.CountByGymAsync(gymId, ct)
            : 0;

        _logger.LogDebug(
            "Affiliation data loaded for {UserId}: {RegionCount} regions, {GymCount} gyms, {DivisionCount} divisions",
            userId,
            regions.Count,
            gyms.Count,
            divisions.Count);

        return new UserAffiliationDto(
            (int)user.Id,
            user.Username,
            regionId,
            user.GymId,
            user.DivisionId,
            membersCount,
            regions,
            gyms,
            divisions);
    }

    public async Task<OperationResult> UpdateAffiliationAsync(UpdateAffiliationRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            _logger.LogWarning("Affiliation update rejected: request is null");
            return Fail(ErrorCodes.ValidationFailed, nameof(request), "Запит не може бути порожнім.");
        }

        var username = (request.Username ?? string.Empty).Trim();

        var usernameError = UsernameRules.GetError(username);
        if (usernameError is not null)
        {
            var invalid = Fail(ErrorCodes.ValidationFailed, nameof(UpdateAffiliationRequest.Username), usernameError);
            return Reject(invalid, request.UserId);
        }

        var user = await _users.GetByIdAsync(request.UserId, ct);
        if (user is null)
        {
            var missing = Fail(ErrorCodes.UserNotFound, nameof(UpdateAffiliationRequest.UserId), UserNotFoundMessage);
            return Reject(missing, request.UserId);
        }

        var sameName = await _users.GetByUsernameAsync(username, ct);
        if (sameName is not null && sameName.Id != user.Id)
        {
            var taken = Fail(ErrorCodes.UsernameTaken, nameof(UpdateAffiliationRequest.Username), "Це ім'я користувача вже зайняте.");
            return Reject(taken, request.UserId);
        }

        var invalidReference = await ValidateReferencesAsync(request, ct);
        if (invalidReference is not null)
        {
            return Reject(invalidReference, request.UserId);
        }

        var updated = await _users.UpdateAffiliationAsync(request.UserId, username, request.GymId, request.DivisionId, ct);
        if (!updated)
        {
            var notSaved = Fail(ErrorCodes.UserNotFound, nameof(UpdateAffiliationRequest.UserId), UserNotFoundMessage);
            return Reject(notSaved, request.UserId);
        }

        _logger.LogInformation(
            "Affiliation updated for {UserId}: gym {GymId}, division {DivisionId}",
            request.UserId,
            request.GymId,
            request.DivisionId);

        return OperationResult.Success();
    }

    private static OperationResult Fail(string errorCode, string field, string message)
    {
        return OperationResult.Fail(errorCode, field, message);
    }

    private async Task<OperationResult?> ValidateReferencesAsync(UpdateAffiliationRequest request, CancellationToken ct)
    {
        if (request.GymId is { } gymId)
        {
            var gyms = await _users.GetGymsAsync(ct);
            if (!gyms.Any(g => g.Id == gymId))
            {
                return Fail(ErrorCodes.GymNotFound, nameof(UpdateAffiliationRequest.GymId), "Обраного спортзалу не існує.");
            }
        }

        if (request.DivisionId is { } divisionId)
        {
            var divisions = await _users.GetDivisionsAsync(ct);
            if (!divisions.Any(d => d.Id == divisionId))
            {
                return Fail(ErrorCodes.DivisionNotFound, nameof(UpdateAffiliationRequest.DivisionId), "Обраного дивізіону не існує.");
            }
        }

        return null;
    }

    private OperationResult Reject(OperationResult result, int userId)
    {
        _logger.LogWarning(
            "Affiliation update rejected for {UserId}: error {ErrorCode}, field {Field}",
            userId,
            result.ErrorCode,
            result.Field);

        return result;
    }
}
