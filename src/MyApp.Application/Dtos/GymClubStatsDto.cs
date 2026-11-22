namespace MyApp.Application.Dtos;

public sealed record GymClubStatsDto(
    int GymId,
    string GymName,
    int RegionId,
    string RegionName,
    int ActiveAthletes,
    long TotalXp,
    string? LeaderNickname);
