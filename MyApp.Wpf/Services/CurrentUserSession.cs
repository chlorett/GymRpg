namespace MyApp.Wpf.Services;

// Вхід у систему поза межами наших юз-кейсів: Id беруться з seed-даних.
public sealed class CurrentUserSession
{
    public int UserId { get; } = 1;

    public int TrainerId { get; } = 2;
}
