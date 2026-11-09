using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public class GymModerationService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GymModerationService> _logger;

    public GymModerationService(IUserRepository userRepository, ILogger<GymModerationService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<GymRosterOverviewDto> GetGymRosterAsync(int? gymId, int? divisionId, string? searchQuery = null)
    {
        var members = await _userRepository.GetGymRosterAsync(gymId, divisionId);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            members = members
                .Where(m => m.Nickname.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return new GymRosterOverviewDto
        {
            TotalAthletes = members.Count,
            ActiveInLeaderboard = members.Count(m => !m.IsExcludedFromLeaderboard && !m.IsBlocked),
            ExcludedFromLeaderboard = members.Count(m => m.IsExcludedFromLeaderboard),
            Members = members.ToList()
        };
    }

    public async Task<bool> UpdateModerationStatusAsync(UpdateAthleteModerationRequest request)
    {
        if (request.ModeratorId == request.TargetUserId)
        {
            _logger.LogWarning("User {UserId} tried to moderate themselves.", request.ModeratorId);
            return false;
        }

        var moderator = await _userRepository.GetByIdAsync(request.ModeratorId);
        if (moderator == null || moderator.Role == UserRole.User)
        {
            _logger.LogWarning("User {ModeratorId} does not have moderation rights.", request.ModeratorId);
            return false;
        }

        var targetUser = await _userRepository.GetByIdAsync(request.TargetUserId);
        if (targetUser != null && (targetUser.Role == UserRole.Trainer || targetUser.Role == UserRole.Admin))
        {
            _logger.LogWarning("Moderator {ModeratorId} tried to moderate trainer/admin {TargetId}.", request.ModeratorId, request.TargetUserId);
            return false;
        }

        DateTimeOffset? blockedAt = request.IsBlocked ? request.BlockedAt ?? DateTimeOffset.UtcNow : null;

        return await _userRepository.UpdateModerationStatusAsync(
            request.TargetUserId,
            request.IsExcludedFromLeaderboard,
            request.IsBlocked,
            blockedAt);
    }
}