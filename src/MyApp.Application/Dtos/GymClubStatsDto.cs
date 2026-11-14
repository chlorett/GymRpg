namespace MyApp.Application.Dtos;

// Сира агрегація по одному залу з репозиторію: місце й середнє рахує сервіс.
public sealed record GymClubStatsDto(
    int GymId,
    string GymName,
    int RegionId,
    string RegionName,
    int ActiveAthletes,
    long TotalXp,
    string? LeaderNickname);
