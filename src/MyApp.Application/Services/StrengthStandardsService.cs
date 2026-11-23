using System.Globalization;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Application.Services;

public sealed class StrengthStandardsService : IStrengthStandardsService
{
    private const decimal DefaultBodyWeightKg = 75.0m;
    private const decimal AmateurRatioThreshold = 0.75m;
    private const decimal AthleteRatioThreshold = 1.25m;
    private const decimal EliteRatioThreshold = 1.75m;
    private const decimal EliteStepRatio = 0.25m;
    private const int MinTargetReps = 1;
    private const int MaxTargetReps = 20;
    private const decimal DefaultBodyweightEquivalentKg = 25m;
    private const decimal VolumeScaleFactor = 0.1m;
    private const decimal FallbackXpScaleFactor = 0.5m;

    private static readonly HashSet<string> AllowedRankFilters = new(StringComparer.OrdinalIgnoreCase)
    {
        "Усі",
        "all",
        "Початківець",
        "Любитель",
        "Атлет",
        "Еліта",
    };

    private readonly IPersonalRecordRepository _recordRepository;
    private readonly IMuscleLoadRepository _muscleLoadRepository;
    private readonly IUserRepository _userRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly ILogger<StrengthStandardsService> _logger;

    public StrengthStandardsService(
        IPersonalRecordRepository recordRepository,
        IMuscleLoadRepository muscleLoadRepository,
        IUserRepository userRepository,
        IExerciseRepository exerciseRepository,
        ILogger<StrengthStandardsService> logger)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _muscleLoadRepository = muscleLoadRepository ?? throw new ArgumentNullException(nameof(muscleLoadRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _exerciseRepository = exerciseRepository ?? throw new ArgumentNullException(nameof(exerciseRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static decimal CalculateWorkingWeightForTarget1Rm(decimal targetOneRepMaxKg, int targetReps)
    {
        if (targetOneRepMaxKg <= 0m || targetReps <= 0)
        {
            return 0m;
        }

        if (targetReps == 1)
        {
            return Math.Round(targetOneRepMaxKg, 1);
        }

        return Math.Round(targetOneRepMaxKg / (1m + (targetReps / 30m)), 1);
    }

    public static (string RankTitle, string NextRankTitle, decimal NextTargetRatio) ClassifyStrengthRatio(decimal ratio)
    {
        if (ratio < AmateurRatioThreshold)
        {
            return ("Початківець", "Любитель", AmateurRatioThreshold);
        }

        if (ratio < AthleteRatioThreshold)
        {
            return ("Любитель", "Атлет", AthleteRatioThreshold);
        }

        if (ratio < EliteRatioThreshold)
        {
            return ("Атлет", "Еліта", EliteRatioThreshold);
        }

        decimal nextMilestone = Math.Round(ratio + EliteStepRatio, 2);
        return ("Еліта", "Еліта+", nextMilestone);
    }

    public async Task<OperationResult<StrengthStandardsOverviewDto>> GetStrengthStandardsOverviewAsync(
        int userId,
        string? rankFilter = null,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            _logger.LogWarning(
                "Strength standards overview rejected: invalid userId {UserId} [{ErrorCode}]",
                userId,
                ErrorCodes.ValidationFailed);
            return OperationResult.Fail<StrengthStandardsOverviewDto>(
                ErrorCodes.ValidationFailed,
                nameof(userId),
                "Некоректний ідентифікатор користувача.");
        }

        if (!string.IsNullOrWhiteSpace(rankFilter) && !AllowedRankFilters.Contains(rankFilter.Trim()))
        {
            _logger.LogWarning(
                "Strength standards overview rejected for user {UserId}: invalid rankFilter '{RankFilter}' [{ErrorCode}]",
                userId,
                rankFilter,
                ErrorCodes.ValidationFailed);
            return OperationResult.Fail<StrengthStandardsOverviewDto>(
                ErrorCodes.ValidationFailed,
                nameof(rankFilter),
                "Невідомий фільтр силового розряду.");
        }

        _logger.LogDebug(
            "Loading strength standards overview for user {UserId} (rankFilter='{RankFilter}')",
            userId,
            rankFilter ?? string.Empty);

        var user = await _userRepository.GetByIdAsync(userId, ct);
        decimal bodyWeightKg = user?.WeightKg is > 0m ? Math.Round(user.WeightKg.Value, 1) : DefaultBodyWeightKg;

        var allRecords = await _recordRepository.GetAllByUserIdAsync(userId, ct);
        var fairRecords = allRecords.Where(r => !r.IsSuspicious).ToList();
        var exercises = await _exerciseRepository.GetAllAsync(ct);
        var exerciseLookup = exercises.ToDictionary(e => e.Id, e => e.Name);

        var allStandards = BuildExerciseStandards(fairRecords, exerciseLookup, bodyWeightKg);
        decimal bestRatio = allStandards.Count > 0 ? allStandards.Max(s => s.RelativeStrengthRatio) : 0m;
        string overallRank = allStandards.Count > 0
            ? ClassifyStrengthRatio(bestRatio).RankTitle
            : "Початківець";

        var filteredStandards = ApplyRankFilter(allStandards, rankFilter);

        var muscleGroups = await _muscleLoadRepository.GetAllMuscleGroupsAsync(ct);
        var exerciseLinks = await _muscleLoadRepository.GetAllExerciseMuscleGroupsAsync(ct);
        var storedLoads = await _muscleLoadRepository.GetUserMuscleLoadsAsync(userId, ct);

        var effectiveLoads = ResolveEffectiveLoads(userId, muscleGroups, exerciseLinks, storedLoads, fairRecords);
        var (pushShare, pullShare, legsShare, symmetryScore, symmetryAdvice) =
            CalculateSymmetryMetrics(muscleGroups, effectiveLoads);

        _logger.LogInformation(
            "Strength standards overview loaded for user {UserId}: bodyWeight={BodyWeight}kg, bestRatio={BestRatio}x ({OverallRank}), symmetry={Symmetry}%",
            userId,
            bodyWeightKg,
            bestRatio,
            overallRank,
            symmetryScore);

        var overview = new StrengthStandardsOverviewDto(
            AthleteBodyWeightKg: bodyWeightKg,
            BestRelativeRatio: bestRatio,
            OverallStrengthRank: overallRank,
            PushSharePercent: pushShare,
            PullSharePercent: pullShare,
            LegsSharePercent: legsShare,
            SymmetryScorePercent: symmetryScore,
            SymmetryAdviceText: symmetryAdvice,
            ExerciseStandards: filteredStandards);

        return OperationResult.Success(overview);
    }

    public async Task<OperationResult<StrengthTargetPlanDto>> CalculateNextStandardTargetAsync(
        int userId,
        int exerciseId,
        int targetReps = 5,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            return RejectTargetCalculation(
                ErrorCodes.ValidationFailed,
                nameof(userId),
                "Некоректний ідентифікатор користувача.",
                userId,
                exerciseId,
                targetReps);
        }

        if (exerciseId <= 0)
        {
            return RejectTargetCalculation(
                ErrorCodes.ValidationFailed,
                nameof(exerciseId),
                "Оберіть вправу для розрахунку цільового нормативу.",
                userId,
                exerciseId,
                targetReps);
        }

        if (targetReps is < MinTargetReps or > MaxTargetReps)
        {
            return RejectTargetCalculation(
                ErrorCodes.ValidationFailed,
                nameof(targetReps),
                $"Кількість повторень для розрахунку має бути від {MinTargetReps} до {MaxTargetReps}.",
                userId,
                exerciseId,
                targetReps);
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return RejectTargetCalculation(
                ErrorCodes.UserNotFound,
                nameof(userId),
                "Користувача не знайдено.",
                userId,
                exerciseId,
                targetReps);
        }

        var exercise = await _exerciseRepository.GetByIdAsync(exerciseId, ct);
        if (exercise is null)
        {
            return RejectTargetCalculation(
                ErrorCodes.ExerciseNotFound,
                nameof(exerciseId),
                "Вправу не знайдено.",
                userId,
                exerciseId,
                targetReps);
        }

        decimal bodyWeightKg = user.WeightKg is > 0m ? Math.Round(user.WeightKg.Value, 1) : DefaultBodyWeightKg;
        var allRecords = await _recordRepository.GetAllByUserIdAsync(userId, ct);
        var exerciseFairRecords = allRecords
            .Where(r => !r.IsSuspicious && r.ExerciseId == exerciseId)
            .ToList();

        decimal currentBest1Rm = CalculateBestOneRepMaxForRecords(exerciseFairRecords);
        decimal currentRatio = bodyWeightKg > 0m ? Math.Round(currentBest1Rm / bodyWeightKg, 2) : 0m;
        var (currentRank, nextRank, nextRatio) = ClassifyStrengthRatio(currentRatio);

        decimal target1RmKg = Math.Round(bodyWeightKg * nextRatio, 1);
        decimal workingWeightKg = CalculateWorkingWeightForTarget1Rm(target1RmKg, targetReps);

        string summary = string.Create(
            CultureInfo.InvariantCulture,
            $"Для переходу в розряд «{nextRank}» у вправі «{exercise.Name}» (цільовий 1RM: {target1RmKg:0.#} кг) рекомендований робочий підхід: {workingWeightKg:0.#} кг × {targetReps} повт.");

        _logger.LogInformation(
            "Strength target calculated for user {UserId}, exercise {ExerciseId}: rank {CurrentRank}->{NextRank}, target1RM={Target1Rm}kg, working={WorkingWeight}kg x {Reps}",
            userId,
            exerciseId,
            currentRank,
            nextRank,
            target1RmKg,
            workingWeightKg,
            targetReps);

        var plan = new StrengthTargetPlanDto(
            ExerciseId: exerciseId,
            ExerciseName: exercise.Name,
            CurrentRankTitle: currentRank,
            TargetRankTitle: nextRank,
            TargetOneRepMaxKg: target1RmKg,
            TargetReps: targetReps,
            RecommendedWorkingWeightKg: workingWeightKg,
            PlanSummaryText: summary);

        return OperationResult.Success(plan);
    }

    private static List<ExerciseStrengthStandardItemDto> BuildExerciseStandards(
        IReadOnlyList<WorkoutRecord> fairRecords,
        IReadOnlyDictionary<int, string> exerciseLookup,
        decimal bodyWeightKg)
    {
        var grouped = fairRecords.GroupBy(r => r.ExerciseId);
        var list = new List<ExerciseStrengthStandardItemDto>();

        foreach (var group in grouped)
        {
            decimal best1Rm = CalculateBestOneRepMaxForRecords(group.ToList());
            if (best1Rm <= 0m)
            {
                continue;
            }

            string exerciseName = ResolveExerciseName(group.First(), exerciseLookup);
            decimal ratio = bodyWeightKg > 0m ? Math.Round(best1Rm / bodyWeightKg, 2) : 0m;
            var (rankTitle, nextRankTitle, nextTargetRatio) = ClassifyStrengthRatio(ratio);
            decimal target1Rm = Math.Round(bodyWeightKg * nextTargetRatio, 1);
            decimal kgToNext = Math.Max(0m, Math.Round(target1Rm - best1Rm, 1));

            list.Add(new ExerciseStrengthStandardItemDto(
                ExerciseId: group.Key,
                ExerciseName: exerciseName,
                BestOneRepMaxKg: best1Rm,
                BodyWeightKg: bodyWeightKg,
                RelativeStrengthRatio: ratio,
                StrengthRankTitle: rankTitle,
                NextRankTitle: nextRankTitle,
                TargetNextRank1RmKg: target1Rm,
                KgToNextRank: kgToNext));
        }

        return list
            .OrderByDescending(x => x.RelativeStrengthRatio)
            .ThenByDescending(x => x.BestOneRepMaxKg)
            .ThenBy(x => x.ExerciseName)
            .ToList();
    }

    private static decimal CalculateBestOneRepMaxForRecords(IReadOnlyList<WorkoutRecord> records)
    {
        decimal best = 0m;
        foreach (var record in records)
        {
            foreach (var set in record.WorkoutSets ?? Array.Empty<WorkoutSet>())
            {
                decimal estimate = PersonalRecordService.CalculateEpleyOneRepMax(set.Weight, set.Reps);
                if (estimate > best)
                {
                    best = estimate;
                }
            }
        }

        return best;
    }

    private static IReadOnlyList<ExerciseStrengthStandardItemDto> ApplyRankFilter(
        List<ExerciseStrengthStandardItemDto> items,
        string? rankFilter)
    {
        if (string.IsNullOrWhiteSpace(rankFilter))
        {
            return items;
        }

        string normalized = rankFilter.Trim();
        if (normalized.Equals("Усі", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return items;
        }

        return items
            .Where(i => i.StrengthRankTitle.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static (int PushShare, int PullShare, int LegsShare, int SymmetryScore, string Advice)
        CalculateSymmetryMetrics(
            IReadOnlyList<MuscleGroup> muscleGroups,
            IReadOnlyList<MuscleLoad> loads)
    {
        var loadByGroupId = loads
            .GroupBy(l => l.MuscleGroupId)
            .ToDictionary(g => g.Key, g => Math.Max(0m, g.OrderByDescending(x => x.UpdatedAt).First().LoadValue));

        decimal pushLoad = 0m;
        decimal pullLoad = 0m;
        decimal legsLoad = 0m;

        foreach (var group in muscleGroups)
        {
            decimal val = loadByGroupId.GetValueOrDefault(group.Id, 0m);
            string category = ClassifyMuscleCategory(group.Id, group.Name);
            if (category == "Push")
            {
                pushLoad += val;
            }
            else if (category == "Pull")
            {
                pullLoad += val;
            }
            else
            {
                legsLoad += val;
            }
        }

        decimal total = pushLoad + pullLoad + legsLoad;
        if (total <= 0m)
        {
            return (34, 33, 33, 100, "Додайте силові тренування для розрахунку м'язової симетрії Push / Pull / Legs");
        }

        int pushShare = (int)Math.Round(pushLoad * 100m / total);
        int pullShare = (int)Math.Round(pullLoad * 100m / total);
        int legsShare = Math.Max(0, 100 - pushShare - pullShare);

        int deviation = Math.Abs(pushShare - 34) + Math.Abs(pullShare - 33) + Math.Abs(legsShare - 33);
        int symmetryScore = Math.Clamp(100 - (deviation * 3 / 4), 0, 100);

        string advice;
        if (symmetryScore >= 80)
        {
            advice = "Відмінна симетрія — гармонійний баланс Push / Pull / Legs";
        }
        else if (legsShare <= pushShare && legsShare <= pullShare)
        {
            advice = "Рекомендовано додати вправи на ноги та корпус (Legs & Core) для вирівнювання симетрії";
        }
        else if (pullShare <= pushShare)
        {
            advice = "Рекомендовано додати тягові вправи на спину та біцепс (Pull) для балансу антагоністів";
        }
        else
        {
            advice = "Рекомендовано додати жимові вправи на груди, плечі та трицепс (Push)";
        }

        return (pushShare, pullShare, legsShare, symmetryScore, advice);
    }

    private static string ClassifyMuscleCategory(int groupId, string groupName)
    {
        if (groupId is 1 or 4 or 6 ||
            groupName.Contains("Груд", StringComparison.OrdinalIgnoreCase) ||
            groupName.Contains("Плеч", StringComparison.OrdinalIgnoreCase) ||
            groupName.Contains("Трицепс", StringComparison.OrdinalIgnoreCase))
        {
            return "Push";
        }

        if (groupId is 2 or 5 ||
            groupName.Contains("Спин", StringComparison.OrdinalIgnoreCase) ||
            groupName.Contains("Біцепс", StringComparison.OrdinalIgnoreCase))
        {
            return "Pull";
        }

        return "Legs";
    }

    private static IReadOnlyList<MuscleLoad> ResolveEffectiveLoads(
        int userId,
        IReadOnlyList<MuscleGroup> muscleGroups,
        IReadOnlyList<ExerciseMuscleGroup> exerciseLinks,
        IReadOnlyList<MuscleLoad> storedLoads,
        IReadOnlyList<WorkoutRecord> fairRecords)
    {
        var now = DateTimeOffset.UtcNow;
        if (storedLoads.Count == 0)
        {
            return fairRecords.Count > 0
                ? ComputeLoadsFromRecords(userId, muscleGroups, exerciseLinks, fairRecords, now)
                : storedLoads;
        }

        var latestUpdate = storedLoads.Max(l => l.UpdatedAt);
        var newerRecords = fairRecords.Where(r => r.CreatedAt > latestUpdate).ToList();
        if (newerRecords.Count == 0)
        {
            return storedLoads;
        }

        var incremental = ComputeLoadsFromRecords(userId, muscleGroups, exerciseLinks, newerRecords, now)
            .ToDictionary(l => l.MuscleGroupId, l => l.LoadValue);
        var baseByGroup = storedLoads
            .GroupBy(l => l.MuscleGroupId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        return muscleGroups
            .OrderBy(g => g.Id)
            .Select(g =>
            {
                baseByGroup.TryGetValue(g.Id, out var existing);
                decimal baseVal = Math.Max(0m, existing?.LoadValue ?? 0m);
                decimal addVal = Math.Max(0m, incremental.GetValueOrDefault(g.Id, 0m));
                return new MuscleLoad
                {
                    UserId = userId,
                    MuscleGroupId = g.Id,
                    LoadValue = Math.Round(baseVal + addVal, 2),
                    UpdatedAt = now,
                };
            })
            .ToList();
    }

    private static List<MuscleLoad> ComputeLoadsFromRecords(
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

    private OperationResult<StrengthTargetPlanDto> RejectTargetCalculation(
        string errorCode,
        string field,
        string message,
        int userId,
        int exerciseId,
        int targetReps)
    {
        _logger.LogWarning(
            "Strength target calculation rejected for user {UserId} (exercise={ExerciseId}, reps={TargetReps}): [{ErrorCode}] on {Field} — {Message}",
            userId,
            exerciseId,
            targetReps,
            errorCode,
            field,
            message);

        return OperationResult.Fail<StrengthTargetPlanDto>(errorCode, field, message);
    }
}
