using System;

namespace MyApp.Application.DTOs.Moderation
{
    public class GymRosterMemberDto
    {
        public int UserId { get; set; }
        public string Nickname { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public int? GymId { get; set; }
        public string GymName { get; set; } = "Не обрано";
        public int? DivisionId { get; set; }
        public string DivisionName { get; set; } = "Не обрано";
        public int Level { get; set; }
        public int Xp { get; set; }
        public int SuspiciousWorkoutsCount { get; set; }
        public bool IsExcludedFromLeaderboard { get; set; }
        public bool IsBlocked { get; set; }
        public DateTimeOffset? BlockedAt { get; set; }

        public string LeaderboardStatusText => IsBlocked
            ? "Заблокований"
            : (IsExcludedFromLeaderboard ? "Прихований з рейтингу" : "Активний у рейтингу");
    }
}
