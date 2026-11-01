namespace MyApp.Application.Dtos;

public sealed record UserAffiliationDto(
    int UserId,
    string Username,
    int? RegionId,
    int? GymId,
    int? DivisionId,
    int GymMembersCount,
    IReadOnlyList<RegionDto> Regions,
    IReadOnlyList<GymDto> Gyms,
    IReadOnlyList<DivisionDto> Divisions);
