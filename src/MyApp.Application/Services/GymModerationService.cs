using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public sealed class GymModerationService : IGymModerationService
{
    private readonly IUserRepository _users;
    private readonly ILogger<GymModerationService> _logger;
    private readonly TimeProvider _timeProvider;

    public GymModerationService(
        IUserRepository users,
        ILogger<GymModerationService> logger,
        TimeProvider timeProvider)
    {
        _users = users;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<GymRosterOverviewDto> GetGymRosterAsync(
        int? gymId,
        int? divisionId,
        string? searchQuery = null,
        CancellationToken ct = default)
    {
        var roster = await _users.GetGymRosterAsync(gymId, divisionId);

        var query = (searchQuery ?? string.Empty).Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? roster.ToList()
            : roster
                .Where(m =>
                    m.Nickname.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    m.GymName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    m.DivisionName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var overview = new GymRosterOverviewDto
        {
            TotalAthletes = filtered.Count,
            ActiveInLeaderboard = filtered.Count(m => !m.IsBlocked && !m.IsExcludedFromLeaderboard),
            ExcludedFromLeaderboard = filtered.Count(m => m.IsExcludedFromLeaderboard || m.IsBlocked),
            BlockedAthletes = filtered.Count(m => m.IsBlocked),
            Members = filtered,
        };

        _logger.LogDebug(
            "Gym roster loaded (gymId={GymId}, divisionId={DivisionId}, search={Search}): {Total} members",
            gymId,
            divisionId,
            query,
            overview.TotalAthletes);

        return overview;
    }

    public async Task<OperationResult> UpdateModerationStatusAsync(
        UpdateAthleteModerationRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
        {
            _logger.LogWarning("Moderation update rejected: request is null");
            return OperationResult.Fail(
                ErrorCodes.ValidationFailed,
                nameof(request),
                "Запит не може бути порожнім.");
        }

        if (request.ModeratorId <= 0)
        {
            _logger.LogWarning("Moderation update rejected: invalid ModeratorId {ModeratorId}", request.ModeratorId);
            return OperationResult.Fail(
                ErrorCodes.ValidationFailed,
                nameof(UpdateAthleteModerationRequest.ModeratorId),
                "Некоректний ідентифікатор модератора.");
        }

        if (request.TargetUserId <= 0)
        {
            _logger.LogWarning("Moderation update rejected: invalid TargetUserId {TargetUserId}", request.TargetUserId);
            return OperationResult.Fail(
                ErrorCodes.ValidationFailed,
                nameof(UpdateAthleteModerationRequest.TargetUserId),
                "Некоректний ідентифікатор цільового користувача.");
        }

        if (request.ModeratorId == request.TargetUserId)
        {
            _logger.LogWarning(
                "Moderation update rejected: moderator {ModeratorId} attempted self-moderation",
                request.ModeratorId);
            return OperationResult.Fail(
                ErrorCodes.CannotModerateSelf,
                nameof(UpdateAthleteModerationRequest.TargetUserId),
                "Модератор не може змінювати власний статус.");
        }

        var moderator = await _users.GetByIdAsync(request.ModeratorId, ct);
        if (moderator is null || (moderator.Role != UserRole.Trainer && moderator.Role != UserRole.Admin))
        {
            _logger.LogWarning(
                "Moderation update rejected: user {ModeratorId} has no moderation rights",
                request.ModeratorId);
            return OperationResult.Fail(
                ErrorCodes.ModerationAccessDenied,
                nameof(UpdateAthleteModerationRequest.ModeratorId),
                "Доступ заборонено: модерація дозволена лише тренерам або адміністраторам.");
        }

        var targetUser = await _users.GetByIdAsync(request.TargetUserId, ct);
        if (targetUser is null)
        {
            _logger.LogWarning(
                "Moderation update rejected: target user {TargetUserId} not found",
                request.TargetUserId);
            return OperationResult.Fail(
                ErrorCodes.UserNotFound,
                nameof(UpdateAthleteModerationRequest.TargetUserId),
                "Цільового користувача не знайдено.");
        }

        if (targetUser.Role != UserRole.User)
        {
            _logger.LogWarning(
                "Moderation update rejected: moderator {ModeratorId} attempted to moderate privileged user {TargetUserId} ({Role})",
                request.ModeratorId,
                request.TargetUserId,
                targetUser.Role);
            return OperationResult.Fail(
                ErrorCodes.CannotModerateTrainerOrAdmin,
                nameof(UpdateAthleteModerationRequest.TargetUserId),
                "Не можна блокувати або виключати з рейтингу тренера чи адміністратора.");
        }

        bool isExcluded = request.IsBlocked || request.IsExcludedFromLeaderboard;
        DateTimeOffset? blockedAt = request.IsBlocked
            ? (request.BlockedAt ?? _timeProvider.GetUtcNow())
            : null;

        var saved = await _users.UpdateModerationStatusAsync(
            request.TargetUserId,
            isExcluded,
            request.IsBlocked,
            blockedAt);

        if (!saved)
        {
            _logger.LogWarning(
                "Moderation update failed in repository for target user {TargetUserId}",
                request.TargetUserId);
            return OperationResult.Fail(
                ErrorCodes.UserNotFound,
                nameof(UpdateAthleteModerationRequest.TargetUserId),
                "Не вдалося зберегти статус модерації користувача.");
        }

        _logger.LogInformation(
            "Moderation updated by {ModeratorId} for user {TargetUserId}: isExcluded={IsExcluded}, isBlocked={IsBlocked}",
            request.ModeratorId,
            request.TargetUserId,
            isExcluded,
            request.IsBlocked);

        return OperationResult.Success();
    }
}
