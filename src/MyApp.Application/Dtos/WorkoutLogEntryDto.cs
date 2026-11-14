namespace MyApp.Application.Dtos;

public sealed record WorkoutLogEntryDto(
    int RecordId,
    int ExerciseId,
    string ExerciseName,
    int SetsCount,
    int TotalReps,
    decimal BestWeightKg,
    decimal TotalVolumeKg,
    decimal BestEstimatedOneRepMaxKg,
    int XpEarned,
    bool IsSuspicious,
    string StatusText,
    string SetsSummaryText,
    DateTimeOffset CreatedAt);
