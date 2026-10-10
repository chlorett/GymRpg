namespace MyApp.Domain.Entities;

public class WorkoutTemplateExercise
{
    public int Id { get; set; }

    public int WorkoutTemplateId { get; set; }

    public int ExerciseId { get; set; }

    public int TargetSets { get; set; }

    public int TargetReps { get; set; }
}
