namespace MyApp.Wpf.Services;

public sealed class CurrentUserSession
{
    public event Action? SessionChanged;

    public int UserId { get; set; } = 1;

    public string UserName { get; set; } = "athlete";

    public int? GymId { get; set; }

    public int? DivisionId { get; set; }

    public int TrainerId { get; set; } = 2;

    public string TrainerName { get; set; } = "coach_ivan";

    public void SetUser(int userId, string userName)
    {
        UserId = userId;
        UserName = userName;
        GymId = null;
        DivisionId = null;
        SessionChanged?.Invoke();
    }

    public void SetAffiliation(string userName, int? gymId, int? divisionId)
    {
        UserName = userName;
        GymId = gymId;
        DivisionId = divisionId;
        SessionChanged?.Invoke();
    }
}
