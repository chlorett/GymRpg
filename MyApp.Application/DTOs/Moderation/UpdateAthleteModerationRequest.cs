namespace MyApp.Application.DTOs.Moderation
{
    public class UpdateAthleteModerationRequest
    {
        public int ModeratorId { get; set; }
        public int TargetUserId { get; set; }
        public bool IsExcludedFromLeaderboard { get; set; }
        public bool IsBlocked { get; set; }
    }
}
