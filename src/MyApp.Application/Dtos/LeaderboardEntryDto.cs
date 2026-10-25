namespace MyApp.Application.Dtos;

public sealed record LeaderboardEntryDto(
    int Rank,
    int UserId,
    string Username,
    int Level,
    long TotalXp,
    string RankTitle,
    bool IsCurrentUser);
