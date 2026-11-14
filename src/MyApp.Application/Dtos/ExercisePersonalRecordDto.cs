namespace MyApp.Application.Dtos;

public sealed record ExercisePersonalRecordDto(
    int ExerciseId,
    string ExerciseName,
    decimal MaxWeightKg,
    decimal MaxVolumeKg,
    decimal EstimatedOneRepMaxKg,
    int TotalWorkoutsCount,
    DateTimeOffset? AchievedAt);
