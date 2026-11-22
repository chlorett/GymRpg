namespace MyApp.Application.Dtos;

public sealed record DivisionPromotionResultDto(
    int? PreviousDivisionId,
    string PreviousDivisionName,
    int NewDivisionId,
    string NewDivisionName,
    long CurrentXp,
    int CurrentLevel);
