using System.Globalization;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Application.Services;

public sealed class MuscleLoadService : IMuscleLoadService
{
    private const int MaxWorkoutHistoryLookup = 500;
    private const decimal DefaultBodyweightEquivalentKg = 25m;
    private const decimal VolumeScaleFactor = 0.1m;
    private const decimal FallbackXpScaleFactor = 0.5m;

    private readonly IMuscleLoadRepository _muscleLoadRepository;
    private readonly IWorkoutRecordRepository _workoutRecordRepository;
    private readonly IUserRepository _userRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MuscleLoadService> _logger;

    public MuscleLoadService(
        IMuscleLoadRepository muscleLoadRepository,
        IWorkoutRecordRepository workoutRecordRepository,
        IUserRepository userRepository,
        TimeProvider timeProvider,
        ILogger<MuscleLoadService> logger)
    {
        _muscleLoadRepository = muscleLoadRepository ?? throw new ArgumentNullException(nameof(muscleLoadRepository));
        _workoutRecordRepository = workoutRecordRepository ?? throw new ArgumentNullException(nameof(workoutRecordRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static string BuildStatusText(decimal loadValue, int sharePercent)
    {
        if (loadValue <= 0m)
        {
            return "Без навантаження";
        }

        if (sharePercent >= 25 || loadValue >= 350m)
        {
            return "Високе навантаження";
        }

        if (sharePercent >= 12 || loadValue >= 100m)
        {
            return "Оптимальний баланс";
        }

        return "Потребує уваги";
    }

    public async Task<MuscleLoadOverviewDto> GetUserMuscleLoadOverviewAsync(
        int userId,
        string? statusFilter = null,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            _logger.LogWarning(
                "Muscle load overview requested with invalid userId: {UserId} [{ErrorCode}]",
                userId,
                ErrorCodes.ValidationFailed);
            return new MuscleLoadOverviewDto(0, 0, 0m, "—", "—", Array.Empty<MuscleGroupLoadItemDto>());
        }

        _logger.LogDebug(
            "Loading muscle load overview for user {UserId} with statusFilter '{StatusFilter}'",
            userId,
            statusFilter ?? string.Empty);

        var muscleGroups = await _muscleLoadRepository.GetAllMuscleGroupsAsync(ct);
        var exerciseLinks = await _muscleLoadRepository.GetAllExerciseMuscleGroupsAsync(ct);
        var storedLoads = await _muscleLoadRepository.GetUserMuscleLoadsAsync(userId, ct);

        if (storedLoads.Count == 0)
        {
            var records = await _workoutRecordRepository.GetRecentByUserIdAsync(
                userId,
                MaxWorkoutHistoryLookup,
                ct);
            storedLoads = ComputeMuscleLoads(userId, muscleGroups, exerciseLinks, records, _timeProvider.GetUtcNow());
        }

        var items = BuildGroupItems(muscleGroups, exerciseLinks, storedLoads);
        int activeGroupsCount = items.Count(i => i.LoadValue > 0m);
        decimal totalLoad = Math.Round(items.Sum(i => i.LoadValue), 2);

        string topGroup = items
            .Where(i => i.LoadValue > 0m)
            .OrderByDescending(i => i.LoadValue)
            .ThenBy(i => i.MuscleGroupId)
            .Select(i => i.MuscleGroupName)
            .FirstOrDefault() ?? "—";

        string leastGroup = items
            .OrderBy(i => i.LoadValue)
            .ThenBy(i => i.MuscleGroupId)
            .Select(i => i.MuscleGroupName)
            .FirstOrDefault() ?? "—";

        var filteredItems = ApplyStatusFilter(items, statusFilter);

        _logger.LogInformation(
            "Muscle load overview loaded for user {UserId}: {ActiveCount}/{TotalCount} active groups, totalLoad={TotalLoad}, {ShownCount} shown",
            userId,
            activeGroupsCount,
            items.Count,
            totalLoad,
            filteredItems.Count);

        return new MuscleLoadOverviewDto(
            TotalGroupsCount: items.Count,
            ActiveGroupsCount: activeGroupsCount,
            TotalLoadValue: totalLoad,
            TopMuscleGroupName: topGroup,
            LeastLoadedMuscleGroupName: leastGroup,
            MuscleGroups: filteredItems);
    }

    public async Task<RecalculateMuscleLoadResultDto> RecalculateMuscleLoadAsync(
        int userId,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            return RejectRecalculate(
                ErrorCodes.ValidationFailed,
                nameof(userId),
                "Некоректний ідентифікатор користувача.",
                userId);
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return RejectRecalculate(
                ErrorCodes.UserNotFound,
                nameof(userId),
                "Користувача не знайдено.",
                userId);
        }

        var muscleGroups = await _muscleLoadRepository.GetAllMuscleGroupsAsync(ct);
        var exerciseLinks = await _muscleLoadRepository.GetAllExerciseMuscleGroupsAsync(ct);
        var records = await _workoutRecordRepository.GetRecentByUserIdAsync(
            userId,
            MaxWorkoutHistoryLookup,
            ct);

        var now = _timeProvider.GetUtcNow();
        var calculatedLoads = ComputeMuscleLoads(userId, muscleGroups, exerciseLinks, records, now);

        await _muscleLoadRepository.SaveUserMuscleLoadsAsync(userId, calculatedLoads, ct);

        decimal totalLoad = Math.Round(calculatedLoads.Sum(l => l.LoadValue), 2);
        var groupNamesById = muscleGroups.ToDictionary(g => g.Id, g => g.Name);
        var topLoad = calculatedLoads
            .Where(l => l.LoadValue > 0m)
            .OrderByDescending(l => l.LoadValue)
            .ThenBy(l => l.MuscleGroupId)
            .FirstOrDefault();

        string topMuscleGroupName = topLoad is not null && groupNamesById.TryGetValue(topLoad.MuscleGroupId, out string? name)
            ? name
            : "—";

        _logger.LogInformation(
            "Muscle load recalculated for user {UserId}: {GroupsCount} groups updated, totalLoad={TotalLoad}, topGroup='{TopGroup}'",
            userId,
            calculatedLoads.Count,
            totalLoad,
            topMuscleGroupName);

        return new RecalculateMuscleLoadResultDto(
            OperationResult.Success(),
            calculatedLoads.Count,
            totalLoad,
            topMuscleGroupName);
    }

    private static List<MuscleLoad> ComputeMuscleLoads(
        int userId,
        IReadOnlyList<MuscleGroup> muscleGroups,
        IReadOnlyList<ExerciseMuscleGroup> exerciseLinks,
        IReadOnlyList<WorkoutRecord> records,
        DateTimeOffset now)
    {
        var loadByGroup = muscleGroups.ToDictionary(g => g.Id, _ => 0m);
        var linksByExerciseId = exerciseLinks
            .GroupBy(l => l.ExerciseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var record in records.Where(r => !r.IsSuspicious))
        {
            if (!linksByExerciseId.TryGetValue(record.ExerciseId, out var links))
            {
                continue;
            }

            decimal recordEffort = CalculateRecordEffort(record);
            foreach (var link in links)
            {
                decimal factor = Math.Clamp(link.LoadFactor, 0.1m, 2.0m);
                decimal contribution = Math.Round(recordEffort * factor, 2);
                if (loadByGroup.ContainsKey(link.MuscleGroupId))
                {
                    loadByGroup[link.MuscleGroupId] += contribution;
                }
            }
        }

        return muscleGroups
            .OrderBy(g => g.Id)
            .Select(g => new MuscleLoad
            {
                UserId = userId,
                MuscleGroupId = g.Id,
                LoadValue = Math.Round(loadByGroup.GetValueOrDefault(g.Id, 0m), 2),
                UpdatedAt = now,
            })
            .ToList();
    }

    private static decimal CalculateRecordEffort(WorkoutRecord record)
    {
        if (record.WorkoutSets is { Count: > 0 })
        {
            decimal setsEffort = 0m;
            foreach (var set in record.WorkoutSets)
            {
                int reps = Math.Max(0, (int)set.Reps);
                decimal effectiveWeight = set.Weight > 0m ? set.Weight : DefaultBodyweightEquivalentKg;
                setsEffort += reps * effectiveWeight * VolumeScaleFactor;
            }

            if (setsEffort > 0m)
            {
                return Math.Round(setsEffort, 2);
            }
        }

        return Math.Round(Math.Max(10m, record.XpEarned * FallbackXpScaleFactor), 2);
    }

    private static List<MuscleGroupLoadItemDto> BuildGroupItems(
        IReadOnlyList<MuscleGroup> muscleGroups,
        IReadOnlyList<ExerciseMuscleGroup> exerciseLinks,
        IReadOnlyList<MuscleLoad> userLoads)
    {
        var loadByGroupId = userLoads
            .GroupBy(l => l.MuscleGroupId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        var exerciseCountByGroupId = exerciseLinks
            .GroupBy(l => l.MuscleGroupId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ExerciseId).Distinct().Count());

        decimal totalLoad = muscleGroups.Sum(g =>
            loadByGroupId.TryGetValue(g.Id, out var load) ? Math.Max(0m, load.LoadValue) : 0m);

        var orderedGroups = muscleGroups.OrderBy(g => g.Id).ToList();
        var items = new List<MuscleGroupLoadItemDto>(orderedGroups.Count);

        foreach (var group in orderedGroups)
        {
            bool hasLoad = loadByGroupId.TryGetValue(group.Id, out var stored);
            decimal loadValue = hasLoad ? Math.Round(Math.Max(0m, stored!.LoadValue), 2) : 0m;
            int sharePercent = totalLoad > 0m
                ? (int)Math.Clamp(Math.Round((double)(loadValue * 100m / totalLoad)), 0, 100)
                : 0;
            int exercisesCount = exerciseCountByGroupId.GetValueOrDefault(group.Id, 0);
            string statusText = BuildStatusText(loadValue, sharePercent);
            string loadText = string.Create(
                CultureInfo.InvariantCulture,
                $"{loadValue:F1} од. ({sharePercent}%)");

            items.Add(new MuscleGroupLoadItemDto(
                MuscleGroupId: group.Id,
                MuscleGroupName: group.Name,
                LoadValue: loadValue,
                SharePercent: sharePercent,
                ExercisesCount: exercisesCount,
                StatusText: statusText,
                LoadText: loadText,
                UpdatedAt: stored?.UpdatedAt));
        }

        return items;
    }

    private static IReadOnlyList<MuscleGroupLoadItemDto> ApplyStatusFilter(
        List<MuscleGroupLoadItemDto> items,
        string? statusFilter)
    {
        if (string.IsNullOrWhiteSpace(statusFilter))
        {
            return items;
        }

        string normalized = statusFilter.Trim();
        if (normalized.Equals("active", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Активні", StringComparison.OrdinalIgnoreCase))
        {
            return items.Where(i => i.LoadValue > 0m).ToList();
        }

        if (normalized.Equals("low", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Потребують уваги", StringComparison.OrdinalIgnoreCase))
        {
            return items
                .Where(i => i.LoadValue <= 0m || i.StatusText == "Потребує уваги")
                .ToList();
        }

        return items;
    }

    private RecalculateMuscleLoadResultDto RejectRecalculate(
        string errorCode,
        string field,
        string message,
        int userId)
    {
        _logger.LogWarning(
            "Muscle load recalculation rejected for user {UserId}: [{ErrorCode}] on {Field} — {Message}",
            userId,
            errorCode,
            field,
            message);

        var failure = OperationResult.Fail(errorCode, field, message);
        return new RecalculateMuscleLoadResultDto(failure, 0, 0m, "—");
    }
}
