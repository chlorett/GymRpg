using MyApp.Domain.Enums;

namespace MyApp.Application.Dtos;

public sealed record SessionExecutionPlanDto(
    int SessionId,
    string WorkoutName,
    DateTime ScheduledDate,
    SessionStatus Status,
    string StatusText,
    IReadOnlyList<SessionExecutionExerciseDto> Exercises);