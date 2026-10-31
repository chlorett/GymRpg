namespace MyApp.Application.Dtos;

public sealed record TemplateExerciseItemDto(
    int Position,
    int ExerciseId,
    string ExerciseName,
    string EquipmentTypeText,
    int TargetSets,
    int TargetReps,
    string SchemeText);
