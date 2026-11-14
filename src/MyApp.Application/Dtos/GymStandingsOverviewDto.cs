namespace MyApp.Application.Dtos;

public sealed record GymStandingsOverviewDto(
    IReadOnlyList<GymStandingDto> Standings,
    IReadOnlyList<RegionDto> Regions,
    IReadOnlyList<DivisionDto> Divisions,
    bool CanRegisterGyms);
