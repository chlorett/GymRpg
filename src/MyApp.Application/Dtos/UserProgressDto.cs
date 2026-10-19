namespace MyApp.Application.Dtos;

// Повний RPG-профіль атлета: рівень, XP, прогрес до наступного рівня та останні тренування.
public sealed record UserProgressDto(
    int UserId,
    string Username,
    int Level,
    long TotalXp,
    long XpForCurrentLevel,
    long XpForNextLevel,
    int WorkoutsCount,
    IReadOnlyList<WorkoutHistoryItemDto> RecentWorkouts);
