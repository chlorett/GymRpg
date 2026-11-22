namespace MyApp.Application.Dtos;

public sealed record DivisionStandingEntryDto(
    int Rank,
    int UserId,
    string Username,
    string GymName,
    long TotalXp,
    int Level,
    bool IsCurrentUser);
