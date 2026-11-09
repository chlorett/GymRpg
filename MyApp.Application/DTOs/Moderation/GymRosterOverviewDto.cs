using System.Collections.Generic;

namespace MyApp.Application.DTOs.Moderation
{
    public class GymRosterOverviewDto
    {
        public int TotalAthletes { get; set; }
        public int ActiveInLeaderboard { get; set; }
        public int ExcludedFromLeaderboard { get; set; }
        public int BlockedAthletes { get; set; }
        public IReadOnlyList<GymRosterMemberDto> Members { get; set; } = new List<GymRosterMemberDto>();
    }
}
