namespace MyApp.Application.Dtos;

public sealed record DivisionQualificationOverviewDto(
    int? CurrentDivisionId,
    string CurrentDivisionName,
    long CurrentXp,
    int CurrentLevel,
    int? NextDivisionId,
    string? NextDivisionName,
    long XpToNextDivision,
    bool CanPromote,
    int SelectedDivisionId,
    string SelectedDivisionName,
    IReadOnlyList<DivisionRequirementDto> Divisions,
    IReadOnlyList<DivisionStandingEntryDto> Standings);
