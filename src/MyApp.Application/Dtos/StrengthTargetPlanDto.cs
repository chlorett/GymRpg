namespace MyApp.Application.Dtos;

public sealed record StrengthTargetPlanDto(
    int ExerciseId,
    string ExerciseName,
    string CurrentRankTitle,
    string TargetRankTitle,
    decimal TargetOneRepMaxKg,
    int TargetReps,
    decimal RecommendedWorkingWeightKg,
    string PlanSummaryText);
