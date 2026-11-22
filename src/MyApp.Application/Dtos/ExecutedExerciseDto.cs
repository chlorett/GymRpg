namespace MyApp.Application.Dtos;

public sealed record ExecutedExerciseDto(
    int ExerciseId,
    IReadOnlyList<WorkoutSetDto> Sets);