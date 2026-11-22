namespace MyApp.Application.Dtos;

public sealed record SessionExecutionSummaryDto(
    int SessionId,
    int EarnedXp,
    int NewLevel,
    int CompletedExercisesCount);