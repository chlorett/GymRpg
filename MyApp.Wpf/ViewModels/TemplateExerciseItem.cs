namespace MyApp.Wpf.ViewModels;

public sealed record TemplateExerciseItem(int ExerciseId, string ExerciseName, int TargetSets, int TargetReps)
{
    public string Summary => $"{ExerciseName}: {TargetSets} × {TargetReps}";
}