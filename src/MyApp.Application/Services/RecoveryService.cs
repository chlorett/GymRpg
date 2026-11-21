using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Application.Services;

public sealed class RecoveryService : IRecoveryService
{
    private const int MinPeriodDays = 1;
    private const int MaxPeriodDays = 365;
    private const int MinReductionPercent = 10;
    private const int MaxReductionPercent = 100;
    private const decimal MaxFatigueLoadThreshold = 400m;
    private const decimal DefaultBodyweightEquivalentKg = 25m;
    private const decimal VolumeScaleFactor = 0.1m;
    private const decimal FallbackXpScaleFactor = 0.5m;

    private readonly IPersonalRecordRepository _recordRepository;
    private readonly IMuscleLoadRepository _muscleLoadRepository;
    private readonly IUserRepository _userRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RecoveryService> _logger;

    public RecoveryService(
        IPersonalRecordRepository recordRepository,
        IMuscleLoadRepository muscleLoadRepository,
        IUserRepository userRepository,
        TimeProvider timeProvider,
        ILogger<RecoveryService> logger)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _muscleLoadRepository = muscleLoadRepository ?? throw new ArgumentNullException(nameof(muscleLoadRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static (int CurrentStreakDays, int BestStreakDays) CalculateStreaks(
        IReadOnlyList<WorkoutRecord> fairRecords,
        DateOnly today)
    {
        if (fairRecords is null || fairRecords.Count == 0)
        {
            return (0, 0);
        }

        var activeDates = fairRecords
            .Where(r => !r.IsSuspicious)
            .Select(r => DateOnly.FromDateTime(r.CreatedAt.UtcDateTime))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (activeDates.Count == 0)
        {
            return (0, 0);
        }

        int bestStreak = 1;
        int runningStreak = 1;

        for (int i = 1; i < activeDates.Count; i++)
        {
            if (activeDates[i] == activeDates[i - 1].AddDays(1))
            {
                runningStreak++;
                if (runningStreak > bestStreak)
                {
                    bestStreak = runningStreak;
                }
            }
            else
            {
                runningStreak = 1;
            }
        }

        var lastDate = activeDates[^1];
        if (lastDate < today.AddDays(-1))
        {
            return (0, bestStreak);
        }

        int currentStreak = 1;
        for (int i = activeDates.Count - 1; i > 0; i--)
        {
            if (activeDates[i] == activeDates[i - 1].AddDays(1))
            {
                currentStreak++;
            }
            else
            {
                break;
            }
        }

        return (currentStreak, bestStreak);
    }

    public static string BuildReadinessSummaryText(int readinessScorePercent)
    {
        if (readinessScorePercent >= 75)
        {
            return "Висока готовність — чудовий час для силових рекордів";
        }

        if (readinessScorePercent >= 45)
        {
            return "Середня готовність — рекомендовано тренувати свіжі групи м'язів";
        }

        return "Висока втома — рекомендовано день відновлення (Deload)";
    }

    public async Task<OperationResult<RecoveryOverviewDto>> GetRecoveryOverviewAsync(
        int userId,
        int periodDays = 7,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            _logger.LogWarning(
                "Recovery overview rejected: invalid userId {UserId} [{ErrorCode}]",
                userId,
                ErrorCodes.ValidationFailed);
            return OperationResult.Fail<RecoveryOverviewDto>(
                ErrorCodes.ValidationFailed,
                nameof(userId),
                "Некоректний ідентифікатор користувача.");
        }

        if (periodDays is < MinPeriodDays or > MaxPeriodDays)
        {
            _logger.LogWarning(
                "Recovery overview rejected for user {UserId}: invalid periodDays {PeriodDays} [{ErrorCode}]",
                userId,
                periodDays,
                ErrorCodes.ValidationFailed);
            return OperationResult.Fail<RecoveryOverviewDto>(
                ErrorCodes.ValidationFailed,
                nameof(periodDays),
                $"Період аналізу має бути від {MinPeriodDays} до {MaxPeriodDays} днів.");
        }

        _logger.LogDebug(
            "Loading recovery overview for user {UserId} (periodDays={PeriodDays})",
            userId,
            periodDays);

        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var cutoff = now.AddDays(-periodDays);

        var allRecords = await _recordRepository.GetAllByUserIdAsync(userId, ct);
        var fairRecords = allRecords.Where(r => !r.IsSuspicious).ToList();

        var (currentStreak, bestStreak) = CalculateStreaks(fairRecords, today);

        var periodRecords = fairRecords.Where(r => r.CreatedAt >= cutoff).ToList();
        int workoutsInPeriod = periodRecords.Count;
        int totalSetsInPeriod = periodRecords.Sum(r => r.WorkoutSets?.Count ?? 0);
        decimal totalVolumeInPeriod = Math.Round(
            periodRecords
                .SelectMany(r => r.WorkoutSets ?? Array.Empty<WorkoutSet>())
                .Sum(s => s.Weight * Math.Max((short)0, s.Reps)),
            1);
        int earnedXpInPeriod = periodRecords.Sum(r => Math.Max(0, r.XpEarned));

        var muscleGroups = await _muscleLoadRepository.GetAllMuscleGroupsAsync(ct);
        var exerciseLinks = await _muscleLoadRepository.GetAllExerciseMuscleGroupsAsync(ct);
        var storedLoads = await _muscleLoadRepository.GetUserMuscleLoadsAsync(userId, ct);

        if (storedLoads.Count == 0 && fairRecords.Count > 0)
        {
            storedLoads = ComputeMuscleLoads(userId, muscleGroups, exerciseLinks, fairRecords, now);
        }

        var recoveryItems = BuildRecoveryItems(muscleGroups, storedLoads);
        int readinessScore = CalculateOverallReadiness(recoveryItems);
        string summaryText = BuildReadinessSummaryText(readinessScore);

        _logger.LogInformation(
            "Recovery overview loaded for user {UserId}: streak={CurrentStreak}/{BestStreak}, periodWorkouts={WorkoutsInPeriod}, readiness={Readiness}%",
            userId,
            currentStreak,
            bestStreak,
            workoutsInPeriod,
            readinessScore);

        var overview = new RecoveryOverviewDto(
            PeriodDays: periodDays,
            CurrentStreakDays: currentStreak,
            BestStreakDays: bestStreak,
            WorkoutsInPeriod: workoutsInPeriod,
            TotalSetsInPeriod: totalSetsInPeriod,
            TotalVolumeInPeriodKg: totalVolumeInPeriod,
            EarnedXpInPeriod: earnedXpInPeriod,
            ReadinessScorePercent: readinessScore,
            ReadinessSummaryText: summaryText,
            MuscleRecoveryItems: recoveryItems);

        return OperationResult.Success(overview);
    }

    public async Task<OperationResult<DeloadResultDto>> ApplyRecoveryDeloadAsync(
        int userId,
        int reductionPercent = 30,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            return RejectDeload(
                ErrorCodes.ValidationFailed,
                nameof(userId),
                "Некоректний ідентифікатор користувача.",
                userId,
                reductionPercent);
        }

        if (reductionPercent is < MinReductionPercent or > MaxReductionPercent)
        {
            return RejectDeload(
                ErrorCodes.ValidationFailed,
                nameof(reductionPercent),
                $"Відсоток зниження втоми має бути від {MinReductionPercent}% до {MaxReductionPercent}%.",
                userId,
                reductionPercent);
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return RejectDeload(
                ErrorCodes.UserNotFound,
                nameof(userId),
                "Користувача не знайдено.",
                userId,
                reductionPercent);
        }

        var now = _timeProvider.GetUtcNow();
        var muscleGroups = await _muscleLoadRepository.GetAllMuscleGroupsAsync(ct);
        var exerciseLinks = await _muscleLoadRepository.GetAllExerciseMuscleGroupsAsync(ct);
        var storedLoads = await _muscleLoadRepository.GetUserMuscleLoadsAsync(userId, ct);

        if (storedLoads.Count == 0)
        {
            var records = await _recordRepository.GetAllByUserIdAsync(userId, ct);
            var fairRecords = records.Where(r => !r.IsSuspicious).ToList();
            storedLoads = ComputeMuscleLoads(userId, muscleGroups, exerciseLinks, fairRecords, now);
        }

        var loadByGroupId = storedLoads
            .GroupBy(l => l.MuscleGroupId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First().LoadValue);

        decimal previousTotalLoad = Math.Round(
            muscleGroups.Sum(g => Math.Max(0m, loadByGroupId.GetValueOrDefault(g.Id, 0m))),
            2);

        decimal multiplier = (100m - reductionPercent) / 100m;
        var updatedLoads = muscleGroups
            .OrderBy(g => g.Id)
            .Select(g =>
            {
                decimal current = Math.Max(0m, loadByGroupId.GetValueOrDefault(g.Id, 0m));
                decimal reduced = Math.Round(current * multiplier, 2);
                return new MuscleLoad
                {
                    UserId = userId,
                    MuscleGroupId = g.Id,
                    LoadValue = reduced,
                    UpdatedAt = now,
                };
            })
            .ToList();

        await _muscleLoadRepository.SaveUserMuscleLoadsAsync(userId, updatedLoads, ct);

        decimal newTotalLoad = Math.Round(updatedLoads.Sum(l => l.LoadValue), 2);
        var updatedItems = BuildRecoveryItems(muscleGroups, updatedLoads);
        int newReadiness = CalculateOverallReadiness(updatedItems);

        _logger.LogInformation(
            "Recovery deload (-{ReductionPercent}%) applied for user {UserId}: load {PrevLoad} -> {NewLoad}, readiness={NewReadiness}%",
            reductionPercent,
            userId,
            previousTotalLoad,
            newTotalLoad,
            newReadiness);

        return OperationResult.Success(new DeloadResultDto(
            UpdatedGroupsCount: updatedLoads.Count,
            ReductionPercent: reductionPercent,
            PreviousTotalLoad: previousTotalLoad,
            NewTotalLoad: newTotalLoad,
            NewReadinessScorePercent: newReadiness));
    }

    private static List<MuscleRecoveryStatusItemDto> BuildRecoveryItems(
        IReadOnlyList<MuscleGroup> muscleGroups,
        IReadOnlyList<MuscleLoad> storedLoads)
    {
        var loadByGroupId = storedLoads
            .GroupBy(l => l.MuscleGroupId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        var items = new List<MuscleRecoveryStatusItemDto>(muscleGroups.Count);
        foreach (var group in muscleGroups.OrderBy(g => g.Id))
        {
            loadByGroupId.TryGetValue(group.Id, out var loadEntity);
            decimal loadValue = Math.Round(Math.Max(0m, loadEntity?.LoadValue ?? 0m), 2);
            int fatiguePercent = (int)Math.Clamp(
                Math.Round((double)(loadValue * 100m / MaxFatigueLoadThreshold)),
                0,
                100);
            int readinessPercent = 100 - fatiguePercent;
            var (statusText, recommendationText) = BuildGroupRecoveryAdvice(fatiguePercent);

            items.Add(new MuscleRecoveryStatusItemDto(
                MuscleGroupId: group.Id,
                MuscleGroupName: group.Name,
                CurrentLoadValue: loadValue,
                FatiguePercent: fatiguePercent,
                ReadinessPercent: readinessPercent,
                RecoveryStatusText: statusText,
                RecommendationText: recommendationText,
                UpdatedAt: loadEntity?.UpdatedAt));
        }

        return items;
    }

    private static (string StatusText, string RecommendationText) BuildGroupRecoveryAdvice(int fatiguePercent)
    {
        if (fatiguePercent >= 70)
        {
            return ("Перевантажена", "Потрібен відпочинок або Deload (48 год)");
        }

        if (fatiguePercent >= 30)
        {
            return ("Оптимальний тонус", "Помірне тренування або базова робота");
        }

        return ("Повністю відновлена", "Готова до важкого силового тренування");
    }

    private static int CalculateOverallReadiness(IReadOnlyList<MuscleRecoveryStatusItemDto> items)
    {
        if (items.Count == 0)
        {
            return 100;
        }

        return (int)Math.Clamp(
            Math.Round(items.Average(i => i.ReadinessPercent)),
            0,
            100);
    }

    private static List<MuscleLoad> ComputeMuscleLoads(
        int userId,
        IReadOnlyList<MuscleGroup> muscleGroups,
        IReadOnlyList<ExerciseMuscleGroup> exerciseLinks,
        IReadOnlyList<WorkoutRecord> fairRecords,
        DateTimeOffset now)
    {
        var loadByGroup = muscleGroups.ToDictionary(g => g.Id, _ => 0m);
        var linksByExerciseId = exerciseLinks
            .GroupBy(l => l.ExerciseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var record in fairRecords.Where(r => !r.IsSuspicious))
        {
            if (!linksByExerciseId.TryGetValue(record.ExerciseId, out var links))
            {
                continue;
            }

            decimal effort = CalculateRecordEffort(record);
            foreach (var link in links)
            {
                decimal factor = Math.Clamp(link.LoadFactor, 0.1m, 2.0m);
                if (loadByGroup.ContainsKey(link.MuscleGroupId))
                {
                    loadByGroup[link.MuscleGroupId] += Math.Round(effort * factor, 2);
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

    private OperationResult<DeloadResultDto> RejectDeload(
        string errorCode,
        string field,
        string message,
        int userId,
        int reductionPercent)
    {
        _logger.LogWarning(
            "Recovery deload rejected for user {UserId} (reduction={ReductionPercent}%): [{ErrorCode}] on {Field} — {Message}",
            userId,
            reductionPercent,
            errorCode,
            field,
            message);

        return OperationResult.Fail<DeloadResultDto>(errorCode, field, message);
    }
}
