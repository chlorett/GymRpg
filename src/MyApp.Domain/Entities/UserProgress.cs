namespace MyApp.Domain.Entities;

public class UserProgress
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int Xp { get; set; }

    public int Level { get; set; } = 1;
}
