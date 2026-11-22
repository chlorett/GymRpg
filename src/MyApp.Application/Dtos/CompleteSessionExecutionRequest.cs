namespace MyApp.Application.Dtos;

public sealed record CompleteSessionExecutionRequest(
    int UserId,
    int SessionId,
    IReadOnlyList<ExecutedExerciseDto> Exercises);