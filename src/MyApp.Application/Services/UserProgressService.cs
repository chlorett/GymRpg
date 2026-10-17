using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Rules;
using MyApp.Domain.Entities;

namespace MyApp.Application.Services;

public sealed class UserProgressService : IUserProgressService
{
    private readonly IUserRepository _users;
    private readonly IUserProgressRepository _progress;
    private readonly IWorkoutRecordRepository _records;
    private readonly IExerciseRepository _exercises;
    private readonly ILogger<UserProgressService> _logger;

    public UserProgressService(
        IUserRepository users,
        IUserProgressRepository progress,
        IWorkoutRecordRepository records,
        IExerciseRepository exercises,
        ILogger<UserProgressService> logger)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _records = records ?? throw new ArgumentNullException(nameof(records));
        _exercises = exercises ?? throw new ArgumentNullException(nameof(exercises));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserProgressDto?> GetProgressAsync(int userId, CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            _logger.LogWarning("Invalid userId requested for progress: {UserId}", userId);
            return null;
        }

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            _logger.LogWarning("User not found when loading progress: {UserId}", userId);
            return null;
        }

        var userProgress = await _progress.GetByUserIdAsync(userId, ct);
        var recentRecords = await _records.GetRecentByUserIdAsync(userId, 20, ct);
        var allExercises = await _exercises.GetAllAsync(ct);
        var exerciseLookup = allExercises.ToDictionary(e => e.Id, e => e.Name);

        long totalXp = userProgress?.Xp ?? 0;
        int level = userProgress is not null && userProgress.Level > 0
            ? userProgress.Level
            : XpCalculator.CalculateLevel((int)totalXp);

        long xpForCurrentLevel = totalXp % WorkoutRules.XpPerLevel;
        long xpForNextLevel = WorkoutRules.XpPerLevel;

        var historyItems = recentRecords
            .Select(r => ToHistoryItem(r, exerciseLookup))
            .ToList();

        _logger.LogInformation(
            "Loaded progress for user {UserId}: Level {Level}, XP {TotalXp}, Workouts {Count}",
            userId,
            level,
            totalXp,
            historyItems.Count);

        return new UserProgressDto(
            userId,
            user.Username,
            level,
            totalXp,
            xpForCurrentLevel,
            xpForNextLevel,
            historyItems.Count,
            historyItems);
    }

    private static WorkoutHistoryItemDto ToHistoryItem(
        WorkoutRecord record,
        IReadOnlyDictionary<int, string> exerciseLookup)
    {
        string exerciseName = ResolveExerciseName(record, exerciseLookup);

        var sets = record.WorkoutSets ?? Array.Empty<WorkoutSet>();
        int setsCount = sets.Count;
        int totalReps = sets.Sum(s => (int)s.Reps);
        decimal totalWeight = sets.Sum(s => s.Weight * s.Reps);

        return new WorkoutHistoryItemDto(
            (int)record.Id,
            exerciseName,
            setsCount,
            totalReps,
            totalWeight,
            record.XpEarned,
            record.CreatedAt);
    }

    private static string ResolveExerciseName(
        WorkoutRecord record,
        IReadOnlyDictionary<int, string> exerciseLookup)
    {
        if (!string.IsNullOrWhiteSpace(record.Exercise?.Name))
        {
            return record.Exercise.Name;
        }

        if (exerciseLookup.TryGetValue(record.ExerciseId, out var name))
        {
            return name;
        }

        return $"Вправа #{record.ExerciseId}";
    }
}
