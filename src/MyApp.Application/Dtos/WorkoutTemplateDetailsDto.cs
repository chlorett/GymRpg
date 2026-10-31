namespace MyApp.Application.Dtos;

public sealed record WorkoutTemplateDetailsDto(
    int TemplateId,
    int TrainerId,
    string Name,
    string Description,
    int ExercisesCount,
    int TotalSetsCount,
    int TotalVolumeReps,
    DateTimeOffset CreatedAt,
    string CreatedAtText,
    IReadOnlyList<TemplateExerciseItemDto> Exercises);
