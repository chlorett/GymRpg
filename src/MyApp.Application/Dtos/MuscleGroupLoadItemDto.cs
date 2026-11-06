namespace MyApp.Application.Dtos;

public sealed record MuscleGroupLoadItemDto(
    short MuscleGroupId,
    string MuscleGroupName,
    decimal LoadValue,
    int SharePercent,
    int ExercisesCount,
    string StatusText,
    string LoadText,
    DateTimeOffset? UpdatedAt);
