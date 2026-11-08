using MyApp.Domain.Enums;

namespace MyApp.Application.Dtos;

public sealed record WorkoutSessionItemDto(
    int SessionId,
    int WorkoutId,
    string WorkoutName,
    DateTime ScheduledDate,
    SessionStatus Status,
    string StatusText,
    DateTimeOffset? CompletedAt,
    int ExercisesCount);