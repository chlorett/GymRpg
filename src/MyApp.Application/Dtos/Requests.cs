using MyApp.Domain.Enums;

namespace MyApp.Application.Dtos;

public sealed record RegisterRequest(
    string Email,
    string Username,
    string Password,
    string ConfirmPassword,
    decimal WeightKg,
    decimal HeightCm,
    DateOnly BirthDate);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record ChangePasswordRequest(
    int UserId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);

public sealed record WorkoutSetDto(int Reps, decimal Weight);

public sealed record TrackWorkoutRequest(
    int UserId,
    int ExerciseId,
    IReadOnlyList<WorkoutSetDto> Sets);

public sealed record TemplateExerciseDto(int ExerciseId, int TargetSets, int TargetReps);

public sealed record CreateTemplateRequest(
    int TrainerId,
    string Name,
    IReadOnlyList<TemplateExerciseDto> Exercises);

public sealed record UpdateProfileRequest(
    int UserId,
    decimal? WeightKg,
    decimal? HeightCm,
    DateOnly? BirthDate);

public sealed record UpdateAffiliationRequest(
    int UserId,
    string Username,
    int? GymId,
    int? DivisionId);

public sealed record ScheduleWorkoutSessionRequest(
    int UserId, int TemplateId, string Name, DateTime ScheduledDate);

public sealed record UpdateSessionStatusRequest(
    int UserId, int SessionId, SessionStatus NewStatus);