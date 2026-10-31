namespace MyApp.Application.Dtos;

public sealed record UserMedalsOverviewDto(
    int TotalMedalsCount,
    int EarnedMedalsCount,
    int AvailableToClaimCount,
    int CompletionPercent,
    IReadOnlyList<MedalItemDto> Medals);
