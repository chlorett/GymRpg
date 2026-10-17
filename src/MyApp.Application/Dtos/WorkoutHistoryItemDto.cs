namespace MyApp.Application.Dtos;

/// <summary>
/// Один запис із журналу тренувань — відображається в RPG-профілі атлета.
/// </summary>
public sealed record WorkoutHistoryItemDto(
    int RecordId,
    string ExerciseName,
    int SetsCount,
    int TotalReps,
    decimal TotalWeightKg,
    int XpEarned,
    DateTimeOffset RecordedAt);
