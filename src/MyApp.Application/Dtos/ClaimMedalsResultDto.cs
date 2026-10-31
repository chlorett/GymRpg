namespace MyApp.Application.Dtos;

public sealed record ClaimMedalsResultDto(
    OperationResult Result,
    int NewlyClaimedCount,
    IReadOnlyList<string> NewlyClaimedNames);
