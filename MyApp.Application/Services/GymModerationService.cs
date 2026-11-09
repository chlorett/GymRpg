using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyApp.Application.Common;
using MyApp.Application.DTOs.Moderation;
using MyApp.Application.Interfaces;

namespace MyApp.Application.Services
{
    public class GymModerationService : IGymModerationService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<GymModerationService> _logger;

        public GymModerationService(
            IUserRepository userRepository,
            ILogger<GymModerationService> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<OperationResult<GymRosterOverviewDto>> GetGymRosterAsync(
            int? gymId,
            int? divisionId,
            string? searchQuery)
        {
            var roster = await _userRepository.GetGymRosterAsync(gymId, divisionId);

            var athletesOnly = roster
                .Where(u => string.Equals(u.Role, "User", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var query = searchQuery.Trim();
                athletesOnly = athletesOnly
                    .Where(u => u.Nickname != null && u.Nickname.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var overview = new GymRosterOverviewDto
            {
                TotalAthletes = athletesOnly.Count,
                ActiveInLeaderboard = athletesOnly.Count(u => !u.IsExcludedFromLeaderboard && !u.IsBlocked),
                ExcludedFromLeaderboard = athletesOnly.Count(u => u.IsExcludedFromLeaderboard || u.IsBlocked),
                BlockedAthletes = athletesOnly.Count(u => u.IsBlocked),
                Members = athletesOnly
                    .OrderByDescending(u => u.Xp)
                    .ThenBy(u => u.Nickname)
                    .ToList()
            };

            return OperationResult<GymRosterOverviewDto>.Success(overview);
        }

        public async Task<OperationResult<bool>> UpdateModerationStatusAsync(UpdateAthleteModerationRequest request)
        {
            if (request == null)
            {
                _logger.LogWarning(
                    "Moderation validation failed: Request is null. ModeratorId={ModeratorId}, TargetUserId={TargetUserId}, ErrorCode={ErrorCode}, Field={Field}",
                    0, 0, ErrorCodes.ValidationFailed, nameof(request));
                return OperationResult<bool>.Failure(ErrorCodes.ValidationFailed, "Запит не може бути порожнім.", nameof(request));
            }

            if (request.ModeratorId <= 0)
            {
                _logger.LogWarning(
                    "Moderation validation failed: Invalid ModeratorId. ModeratorId={ModeratorId}, TargetUserId={TargetUserId}, ErrorCode={ErrorCode}, Field={Field}",
                    request.ModeratorId, request.TargetUserId, ErrorCodes.ValidationFailed, nameof(request.ModeratorId));
                return OperationResult<bool>.Failure(ErrorCodes.ValidationFailed, "Некоректний ідентифікатор модератора.", nameof(request.ModeratorId));
            }

            if (request.TargetUserId <= 0)
            {
                _logger.LogWarning(
                    "Moderation validation failed: Invalid TargetUserId. ModeratorId={ModeratorId}, TargetUserId={TargetUserId}, ErrorCode={ErrorCode}, Field={Field}",
                    request.ModeratorId, request.TargetUserId, ErrorCodes.ValidationFailed, nameof(request.TargetUserId));
                return OperationResult<bool>.Failure(ErrorCodes.ValidationFailed, "Некоректний ідентифікатор цільового користувача.", nameof(request.TargetUserId));
            }

            if (request.ModeratorId == request.TargetUserId)
            {
                _logger.LogWarning(
                    "Moderation denied: Moderator attempted self-moderation. ModeratorId={ModeratorId}, TargetUserId={TargetUserId}, ErrorCode={ErrorCode}, Field={Field}",
                    request.ModeratorId, request.TargetUserId, ErrorCodes.CannotModerateSelf, nameof(request.TargetUserId));
                return OperationResult<bool>.Failure(ErrorCodes.CannotModerateSelf, "Ви не можете модерувати або блокувати власний обліковий запис.", nameof(request.TargetUserId));
            }

            var moderator = await _userRepository.GetByIdAsync(request.ModeratorId);
            if (moderator == null || !IsTrainerOrAdmin(moderator.Role))
            {
                _logger.LogWarning(
                    "Moderation denied: Moderator lacks required role. ModeratorId={ModeratorId}, TargetUserId={TargetUserId}, ErrorCode={ErrorCode}, Field={Field}",
                    request.ModeratorId, request.TargetUserId, ErrorCodes.ModerationAccessDenied, nameof(request.ModeratorId));
                return OperationResult<bool>.Failure(ErrorCodes.ModerationAccessDenied, "Доступ заборонено. Модерація дозволена лише тренерам та адміністраторам.", nameof(request.ModeratorId));
            }

            var targetUser = await _userRepository.GetByIdAsync(request.TargetUserId);
            if (targetUser == null)
            {
                _logger.LogWarning(
                    "Moderation failed: Target user not found. ModeratorId={ModeratorId}, TargetUserId={TargetUserId}, ErrorCode={ErrorCode}, Field={Field}",
                    request.ModeratorId, request.TargetUserId, ErrorCodes.UserNotFound, nameof(request.TargetUserId));
                return OperationResult<bool>.Failure(ErrorCodes.UserNotFound, "Цільового користувача не знайдено.", nameof(request.TargetUserId));
            }

            if (!string.Equals(targetUser.Role, "User", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Moderation denied: Target user is Trainer or Admin. ModeratorId={ModeratorId}, TargetUserId={TargetUserId}, ErrorCode={ErrorCode}, Field={Field}",
                    request.ModeratorId, request.TargetUserId, ErrorCodes.CannotModerateTrainerOrAdmin, nameof(request.TargetUserId));
                return OperationResult<bool>.Failure(ErrorCodes.CannotModerateTrainerOrAdmin, "Неможливо модерувати або блокувати іншого тренера чи адміністратора.", nameof(request.TargetUserId));
            }

            var effectiveExcluded = request.IsExcludedFromLeaderboard || request.IsBlocked;
            DateTimeOffset? blockedAt = request.IsBlocked
                ? (targetUser.BlockedAt ?? DateTimeOffset.UtcNow)
                : null;

            var updated = await _userRepository.UpdateModerationStatusAsync(
                request.TargetUserId,
                effectiveExcluded,
                request.IsBlocked,
                blockedAt);

            if (!updated)
            {
                _logger.LogWarning(
                    "Moderation update failed in repository. ModeratorId={ModeratorId}, TargetUserId={TargetUserId}, ErrorCode={ErrorCode}, Field={Field}",
                    request.ModeratorId, request.TargetUserId, ErrorCodes.UserNotFound, nameof(request.TargetUserId));
                return OperationResult<bool>.Failure(ErrorCodes.UserNotFound, "Не вдалося оновити статус модерації користувача.", nameof(request.TargetUserId));
            }

            _logger.LogInformation(
                "Moderation status updated successfully. ModeratorId={ModeratorId}, TargetUserId={TargetUserId}, IsExcludedFromLeaderboard={IsExcluded}, IsBlocked={IsBlocked}, BlockedAt={BlockedAt}",
                request.ModeratorId, request.TargetUserId, effectiveExcluded, request.IsBlocked, blockedAt);

            return OperationResult<bool>.Success(true);
        }

        private static bool IsTrainerOrAdmin(string? role) =>
            string.Equals(role, "Trainer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}
