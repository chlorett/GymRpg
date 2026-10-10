namespace MyApp.Domain.Entities;

public class WorkoutTemplate
{
    public int Id { get; set; }

    public int TrainerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<WorkoutTemplateExercise> Exercises { get; set; } = new();
}
