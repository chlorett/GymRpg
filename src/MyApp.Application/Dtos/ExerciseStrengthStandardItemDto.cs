namespace MyApp.Application.Dtos;

public sealed record ExerciseStrengthStandardItemDto(
    int ExerciseId,
    string ExerciseName,
    decimal BestOneRepMaxKg,
    decimal BodyWeightKg,
    decimal RelativeStrengthRatio,
    string StrengthRankTitle,
    string NextRankTitle,
    decimal TargetNextRank1RmKg,
    decimal KgToNextRank);
