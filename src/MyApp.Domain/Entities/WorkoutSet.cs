namespace MyApp.Domain.Entities;

public class WorkoutSet
{
    public int Id { get; set; }

    public int WorkoutRecordId { get; set; }

    public int Reps { get; set; }

    public decimal Weight { get; set; }
}
