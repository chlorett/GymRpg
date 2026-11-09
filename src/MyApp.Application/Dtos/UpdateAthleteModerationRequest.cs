using System;

namespace MyApp.Application.Dtos;

public class UpdateAthleteModerationRequest
{
    public int ModeratorId { get; set; }

    public int TargetUserId { get; set; }

    public bool IsExcludedFromLeaderboard { get; set; }

    public bool IsBlocked { get; set; }

    public DateTimeOffset? BlockedAt { get; set; }
}