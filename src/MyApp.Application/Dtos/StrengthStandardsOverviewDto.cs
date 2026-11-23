namespace MyApp.Application.Dtos;

public sealed record StrengthStandardsOverviewDto(
    decimal AthleteBodyWeightKg,
    decimal BestRelativeRatio,
    string OverallStrengthRank,
    int PushSharePercent,
    int PullSharePercent,
    int LegsSharePercent,
    int SymmetryScorePercent,
    string SymmetryAdviceText,
    IReadOnlyList<ExerciseStrengthStandardItemDto> ExerciseStandards);
