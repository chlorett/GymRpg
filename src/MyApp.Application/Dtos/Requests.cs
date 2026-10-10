namespace MyApp.Application.Dtos;

public sealed record RegisterRequest(
    string Email,
    string Username,
    string Password,
    string ConfirmPassword,
    decimal WeightKg,
    decimal HeightCm,
    DateOnly BirthDate);

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
