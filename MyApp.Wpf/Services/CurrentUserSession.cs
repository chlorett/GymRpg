namespace MyApp.Wpf.Services;

public sealed class CurrentUserSession
{
    public event Action? SessionChanged;

    public int UserId { get; set; } = 1;

    public string UserName { get; set; } = "athlete";

    public int TrainerId { get; set; } = 2;

    public string TrainerName { get; set; } = "coach_ivan";

    public void SetUser(int userId, string userName)
    {
        UserId = userId;
        UserName = userName;
        SessionChanged?.Invoke();
    }
}
