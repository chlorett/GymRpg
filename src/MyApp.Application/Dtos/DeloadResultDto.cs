namespace MyApp.Application.Dtos;

public sealed record DeloadResultDto(
    int UpdatedGroupsCount,
    int ReductionPercent,
    decimal PreviousTotalLoad,
    decimal NewTotalLoad,
    int NewReadinessScorePercent);
