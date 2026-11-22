namespace MyApp.Application.Dtos;

public sealed record SessionExecutionExerciseDto(
    int ExerciseId,
    string Name,
    string? Technique,
    int Position,
    int TargetSets,
    int TargetReps);
