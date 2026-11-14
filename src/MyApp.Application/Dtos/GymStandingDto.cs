namespace MyApp.Application.Dtos;

public sealed record GymStandingDto(
    int Rank,
    int GymId,
    string GymName,
    string RegionName,
    int ActiveAthletes,
    long TotalXp,
    long AverageXp,
    string LeaderNickname);
