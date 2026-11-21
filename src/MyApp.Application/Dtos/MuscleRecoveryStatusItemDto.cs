namespace MyApp.Application.Dtos;

public sealed record MuscleRecoveryStatusItemDto(
    int MuscleGroupId,
    string MuscleGroupName,
    decimal CurrentLoadValue,
    int FatiguePercent,
    int ReadinessPercent,
    string RecoveryStatusText,
    string RecommendationText,
    DateTimeOffset? UpdatedAt);
