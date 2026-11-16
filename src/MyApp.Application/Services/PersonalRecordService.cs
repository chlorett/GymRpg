using System.Globalization;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Rules;
using MyApp.Domain.Entities;

namespace MyApp.Application.Services;

public sealed class PersonalRecordService : IPersonalRecordService
{
    private readonly IPersonalRecordRepository _recordRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserProgressRepository _progressRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PersonalRecordService> _logger;

    public PersonalRecordService(
        IPersonalRecordRepository recordRepository,
        IUserRepository userRepository,
        IUserProgressRepository progressRepository,
        IExerciseRepository exerciseRepository,
        TimeProvider timeProvider,
        ILogger<PersonalRecordService> logger)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _progressRepository = progressRepository ?? throw new ArgumentNullException(nameof(progressRepository));
        _exerciseRepository = exerciseRepository ?? throw new ArgumentNullException(nameof(exerciseRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static decimal CalculateEpleyOneRepMax(decimal weightKg, int reps)
    {
        if (weightKg <= 0m || reps <= 0)
        {
            return 0m;
        }

        if (reps == 1)
        {
            return Math.Round(weightKg, 1);
        }

        return Math.Round(weightKg * (1m + (reps / 30m)), 1);
    }

    public async Task<OperationResult<PersonalRecordsOverviewDto>> GetPersonalRecordsOverviewAsync(
        int userId,
        int? exerciseIdFilter = null,
        string? honestyFilter = null,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            _logger.LogWarning(
                "Personal records overview requested with invalid userId: {UserId} [{ErrorCode}]",
                userId,
                ErrorCodes.ValidationFailed);

            return OperationResult.Fail<PersonalRecordsOverviewDto>(
                ErrorCodes.ValidationFailed,
                nameof(userId),
                "Некоректний ідентифікатор користувача.");
        }

        if (exerciseIdFilter is <= 0)
        {
            _logger.LogWarning(
                "Personal records overview requested with invalid exerciseIdFilter: {ExerciseIdFilter} [{ErrorCode}]",
                exerciseIdFilter,
                ErrorCodes.ValidationFailed);

            return OperationResult.Fail<PersonalRecordsOverviewDto>(
                ErrorCodes.ValidationFailed,
                nameof(exerciseIdFilter),
                "Некоректний фільтр вправи.");
        }

        _logger.LogDebug(
            "Loading personal records overview for user {UserId} (exerciseFilter={ExerciseId}, honestyFilter='{HonestyFilter}')",
            userId,
            exerciseIdFilter,
            honestyFilter ?? string.Empty);

        var records = await _recordRepository.GetAllByUserIdAsync(userId, ct);
        var exercises = await _exerciseRepository.GetAllAsync(ct);
        var exerciseLookup = exercises.ToDictionary(e => e.Id, e => e.Name);

        var allPersonalRecords = BuildPersonalRecords(records, exerciseLookup);
        decimal bestOneRepMax = allPersonalRecords.Count > 0
            ? allPersonalRecords.Max(p => p.EstimatedOneRepMaxKg)
            : 0m;
        string bestExerciseName = allPersonalRecords
            .OrderByDescending(p => p.EstimatedOneRepMaxKg)
            .ThenByDescending(p => p.MaxWeightKg)
            .Select(p => p.ExerciseName)
            .FirstOrDefault() ?? "—";

        var filteredPersonalRecords = exerciseIdFilter is > 0
            ? allPersonalRecords.Where(p => p.ExerciseId == exerciseIdFilter.Value).ToList()
            : allPersonalRecords;

        var filteredRecordsForLog = ApplyFilters(records, exerciseIdFilter, honestyFilter);
        var workoutLogs = filteredRecordsForLog
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => BuildWorkoutLogEntry(r, exerciseLookup))
            .ToList();

        _logger.LogInformation(
            "Personal records loaded for user {UserId}: {TotalWorkouts} total records, {PrCount} exercise PRs, best 1RM={BestOneRepMax} kg ({BestExercise})",
            userId,
            records.Count,
            allPersonalRecords.Count,
            bestOneRepMax,
            bestExerciseName);

        var overview = new PersonalRecordsOverviewDto(
            TotalWorkoutsLogged: records.Count,
            ExercisesWithRecordsCount: allPersonalRecords.Count,
            BestOneRepMaxKg: bestOneRepMax,
            BestExerciseName: bestExerciseName,
            PersonalRecords: filteredPersonalRecords,
            WorkoutLogs: workoutLogs);

        return OperationResult.Success(overview);
    }

    public async Task<OperationResult<DeleteWorkoutRecordResultDto>> DeleteWorkoutRecordAsync(
        int userId,
        int recordId,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            return RejectDelete(
                ErrorCodes.ValidationFailed,
                nameof(userId),
                "Некоректний ідентифікатор користувача.",
                userId,
                recordId);
        }

        if (recordId <= 0)
        {
            return RejectDelete(
                ErrorCodes.ValidationFailed,
                nameof(recordId),
                "Некоректний ідентифікатор запису тренування.",
                userId,
                recordId);
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return RejectDelete(
                ErrorCodes.UserNotFound,
                nameof(userId),
                "Користувача не знайдено.",
                userId,
                recordId);
        }

        var record = await _recordRepository.GetByIdWithDetailsAsync(recordId, ct);
        if (record is null)
        {
            return RejectDelete(
                ErrorCodes.RecordNotFound,
                nameof(recordId),
                "Запис тренування не знайдено.",
                userId,
                recordId);
        }

        long ownerId = record.Session?.Workout?.UserId ?? 0;
        if (ownerId != 0 && ownerId != userId)
        {
            return RejectDelete(
                ErrorCodes.RecordAccessDenied,
                nameof(recordId),
                "Ви не можете видалити чужий запис тренування.",
                userId,
                recordId);
        }

        bool deleted = await _recordRepository.DeleteRecordAsync(recordId, ct);
        if (!deleted)
        {
            return RejectDelete(
                ErrorCodes.RecordNotFound,
                nameof(recordId),
                "Не вдалося видалити запис тренування.",
                userId,
                recordId);
        }

        var progress = await _progressRepository.GetByUserIdAsync(userId, ct);
        int adjustedXp = (int)(progress?.Xp ?? 0);
        int newLevel = progress?.Level ?? 1;

        if (progress is not null && !record.IsSuspicious && record.XpEarned > 0)
        {
            progress.Xp = Math.Max(0, progress.Xp - record.XpEarned);
            progress.Level = XpCalculator.CalculateLevel((int)progress.Xp);
            progress.UpdatedAt = _timeProvider.GetUtcNow();
            await _progressRepository.SaveAsync(progress, ct);

            adjustedXp = (int)progress.Xp;
            newLevel = progress.Level;
        }

        _logger.LogInformation(
            "Workout record {RecordId} deleted by user {UserId}: adjustedXp={AdjustedXp}, newLevel={NewLevel}",
            recordId,
            userId,
            adjustedXp,
            newLevel);

        return OperationResult.Success(new DeleteWorkoutRecordResultDto(
            recordId,
            adjustedXp,
            newLevel));
    }

    private static List<ExercisePersonalRecordDto> BuildPersonalRecords(
        IReadOnlyList<WorkoutRecord> records,
        IReadOnlyDictionary<int, string> exerciseLookup)
    {
        var fairRecords = records.Where(r => !r.IsSuspicious).ToList();
        var grouped = fairRecords.GroupBy(r => r.ExerciseId);
        var result = new List<ExercisePersonalRecordDto>();

        foreach (var group in grouped)
        {
            string exerciseName = ResolveExerciseName(group.First(), exerciseLookup);
            decimal maxWeight = 0m;
            decimal maxVolume = 0m;
            decimal bestOneRepMax = 0m;
            DateTimeOffset? achievedAt = null;

            foreach (var record in group)
            {
                var sets = record.WorkoutSets ?? Array.Empty<WorkoutSet>();
                decimal recordVolume = 0m;

                foreach (var set in sets)
                {
                    if (set.Weight > maxWeight)
                    {
                        maxWeight = set.Weight;
                    }

                    recordVolume += set.Weight * Math.Max((short)0, set.Reps);
                    decimal setOneRepMax = CalculateEpleyOneRepMax(set.Weight, set.Reps);
                    if (setOneRepMax > bestOneRepMax)
                    {
                        bestOneRepMax = setOneRepMax;
                        achievedAt = record.CreatedAt;
                    }
                }

                if (recordVolume > maxVolume)
                {
                    maxVolume = recordVolume;
                }

                if (achievedAt is null)
                {
                    achievedAt = record.CreatedAt;
                }
            }

            result.Add(new ExercisePersonalRecordDto(
                ExerciseId: group.Key,
                ExerciseName: exerciseName,
                MaxWeightKg: Math.Round(maxWeight, 1),
                MaxVolumeKg: Math.Round(maxVolume, 1),
                EstimatedOneRepMaxKg: bestOneRepMax,
                TotalWorkoutsCount: group.Count(),
                AchievedAt: achievedAt));
        }

        return result
            .OrderByDescending(r => r.EstimatedOneRepMaxKg)
            .ThenByDescending(r => r.MaxVolumeKg)
            .ThenBy(r => r.ExerciseName)
            .ToList();
    }

    private static WorkoutLogEntryDto BuildWorkoutLogEntry(
        WorkoutRecord record,
        IReadOnlyDictionary<int, string> exerciseLookup)
    {
        string exerciseName = ResolveExerciseName(record, exerciseLookup);
        var sets = (record.WorkoutSets ?? Array.Empty<WorkoutSet>())
            .OrderBy(s => s.SetNumber)
            .ToList();

        int setsCount = sets.Count;
        int totalReps = sets.Sum(s => Math.Max(0, (int)s.Reps));
        decimal bestWeight = sets.Count > 0 ? sets.Max(s => s.Weight) : 0m;
        decimal totalVolume = Math.Round(sets.Sum(s => s.Weight * Math.Max((short)0, s.Reps)), 1);
        decimal bestOneRepMax = sets.Count > 0
            ? sets.Max(s => CalculateEpleyOneRepMax(s.Weight, s.Reps))
            : 0m;

        string setsSummary = sets.Count > 0
            ? string.Join(
                ", ",
                sets.Select(s => string.Create(CultureInfo.InvariantCulture, $"{s.Weight:0.#} кг × {s.Reps}")))
            : "Без підходів";

        string statusText = record.IsSuspicious ? "Підозрілий" : "Чесний";

        return new WorkoutLogEntryDto(
            RecordId: (int)record.Id,
            ExerciseId: record.ExerciseId,
            ExerciseName: exerciseName,
            SetsCount: setsCount,
            TotalReps: totalReps,
            BestWeightKg: Math.Round(bestWeight, 1),
            TotalVolumeKg: totalVolume,
            BestEstimatedOneRepMaxKg: bestOneRepMax,
            XpEarned: record.XpEarned,
            IsSuspicious: record.IsSuspicious,
            StatusText: statusText,
            SetsSummaryText: setsSummary,
            CreatedAt: record.CreatedAt);
    }

    private static IEnumerable<WorkoutRecord> ApplyFilters(
        IReadOnlyList<WorkoutRecord> records,
        int? exerciseIdFilter,
        string? honestyFilter)
    {
        IEnumerable<WorkoutRecord> query = records;

        if (exerciseIdFilter is > 0)
        {
            query = query.Where(r => r.ExerciseId == exerciseIdFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(honestyFilter))
        {
            string normalized = honestyFilter.Trim();
            if (normalized.Equals("fair", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Тільки чесні", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => !r.IsSuspicious);
            }
            else if (normalized.Equals("suspicious", StringComparison.OrdinalIgnoreCase) ||
                     normalized.Equals("Підозрілі", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.IsSuspicious);
            }
        }

        return query;
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

    private OperationResult<DeleteWorkoutRecordResultDto> RejectDelete(
        string errorCode,
        string field,
        string message,
        int userId,
        int recordId)
    {
        _logger.LogWarning(
            "Workout record deletion rejected for user {UserId}, record {RecordId}: [{ErrorCode}] on {Field} — {Message}",
            userId,
            recordId,
            errorCode,
            field,
            message);

        return OperationResult.Fail<DeleteWorkoutRecordResultDto>(errorCode, field, message);
    }
}
