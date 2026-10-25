namespace MyApp.Application.Dtos;

public sealed record LeaderboardDto(
    int? CurrentUserRank,
    int CurrentUserLevel,
    long CurrentUserXp,
    int TotalParticipants,
    IReadOnlyList<LeaderboardEntryDto> Entries);
