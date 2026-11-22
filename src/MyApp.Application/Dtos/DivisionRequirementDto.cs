namespace MyApp.Application.Dtos;

public sealed record DivisionRequirementDto(
    int DivisionId,
    string Name,
    long MinXp,
    int MinLevel,
    bool IsCurrent,
    bool IsReached);
