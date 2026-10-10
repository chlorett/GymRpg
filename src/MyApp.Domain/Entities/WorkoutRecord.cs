namespace MyApp.Domain.Entities;

public class WorkoutRecord
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int ExerciseId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public int XpEarned { get; set; }

    public bool IsSuspicious { get; set; }

    public List<WorkoutSet> Sets { get; set; } = new();
}
