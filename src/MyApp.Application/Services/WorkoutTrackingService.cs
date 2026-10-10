using System.Globalization;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Rules;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public sealed class WorkoutTrackingService : IWorkoutTrackingService
{
    private readonly IUserRepository _users;
    private readonly IExerciseRepository _exercises;
    private readonly IWorkoutRecordRepository _records;
    private readonly IUserProgressRepository _progress;
    private readonly TimeProvider _time;
    private readonly ILogger<WorkoutTrackingService> _logger;

    public WorkoutTrackingService(
            IWorkoutRecordRepository records,
            IUserProgressRepository progress,
            IUserRepository users,
            IExerciseRepository exercises,
            TimeProvider time,
            ILogger<WorkoutTrackingService> logger)
    {
        _users = users;
        _exercises = exercises;
        _records = records;
        _progress = progress;
        _time = time;
        _logger = logger;
    }

    public async Task<OperationResult> TrackAsync(TrackWorkoutRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(request), "Запит не може бути порожнім.");
        }

        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Reject(invalid, request);
        }

        if (await _users.GetByIdAsync(request.UserId, ct) is null)
        {
            var notFound = Fail(ErrorCodes.UserNotFound, nameof(TrackWorkoutRequest.UserId), "Користувача не знайдено.");
            return Reject(notFound, request);
        }

        if (await _exercises.GetByIdAsync(request.ExerciseId, ct) is null)
        {
            var notFound = Fail(ErrorCodes.ExerciseNotFound, nameof(TrackWorkoutRequest.ExerciseId), "Вправу не знайдено.");
            return Reject(notFound, request);
        }

        var record = CreateRecord(request);
        await _records.AddAsync(record, ct);
        await UpdateProgressAsync(request.UserId, record.XpEarned, ct);

        _logger.LogInformation(
            "Workout recorded: user {UserId}, exercise {ExerciseId}, sets {SetCount}, xp {Xp}",
            request.UserId,
            request.ExerciseId,
            request.Sets.Count,
            record.XpEarned);

        if (record.IsSuspicious)
        {
            _logger.LogWarning(
                "Suspicious workout record: user {UserId}, exercise {ExerciseId}",
                request.UserId,
                request.ExerciseId);
        }

        return OperationResult.Success();
    }

    private static OperationResult? Validate(TrackWorkoutRequest request)
    {
        if (request.UserId <= 0)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(TrackWorkoutRequest.UserId), "Некоректний користувач.");
        }

        if (request.ExerciseId <= 0)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(TrackWorkoutRequest.ExerciseId), "Оберіть вправу.");
        }

        if (request.Sets is null || request.Sets.Count == 0)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(TrackWorkoutRequest.Sets), "Додайте хоча б один підхід.");
        }

        if (request.Sets.Count > WorkoutRules.MaxSets)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(TrackWorkoutRequest.Sets), $"Не більше {WorkoutRules.MaxSets} підходів.");
        }

        for (var index = 0; index < request.Sets.Count; index++)
        {
            var error = ValidateSet(request.Sets[index], index + 1);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static OperationResult? ValidateSet(WorkoutSetDto set, int number)
    {
        if (set.Reps is < WorkoutRules.MinReps or > WorkoutRules.MaxReps)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(WorkoutSetDto.Reps),
                $"Підхід №{number}: повторень має бути від {WorkoutRules.MinReps} до {WorkoutRules.MaxReps}.");
        }

        if (set.Weight is < WorkoutRules.MinWeight or > WorkoutRules.MaxWeight)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(WorkoutSetDto.Weight),
                $"Підхід №{number}: вага має бути від {WorkoutRules.MinWeight} до {WorkoutRules.MaxWeight} кг.");
        }

        return null;
    }

    private static OperationResult Fail(string errorCode, string field, string message)
    {
        return OperationResult.Fail(errorCode, field, message);
    }

    private static WorkoutSet ToWorkoutSet(WorkoutSetDto set, int index)
    {
        return new WorkoutSet
        {
            SetNumber = (short)(index + 1),
            Reps = (short)set.Reps,
            Weight = set.Weight,
        };
    }

    private WorkoutRecord CreateRecord(TrackWorkoutRequest request)
    {
        var now = _time.GetUtcNow();

        var workout = new Workout
        {
            UserId = request.UserId,
            Name = $"Тренування {now.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)}",
            CreatedAt = now,
            UpdatedAt = now,
        };

        var session = new WorkoutSession
        {
            Workout = workout,
            ScheduledDate = now.UtcDateTime,
            Status = SessionStatus.Completed,
            CompletedAt = now,
            CreatedAt = now,
        };

        return new WorkoutRecord
        {
            Session = session,
            ExerciseId = request.ExerciseId,
            CreatedAt = now,
            XpEarned = XpCalculator.CalculateXp(request.Sets),
            IsSuspicious = XpCalculator.IsSuspicious(request.Sets),
            WorkoutSets = request.Sets.Select(ToWorkoutSet).ToList(),
        };
    }

    private OperationResult Reject(OperationResult result, TrackWorkoutRequest request)
    {
        _logger.LogWarning(
            "Workout rejected: user {UserId}, exercise {ExerciseId}, error {ErrorCode}, field {Field}",
            request.UserId,
            request.ExerciseId,
            result.ErrorCode,
            result.Field);

        return result;
    }

    private async Task UpdateProgressAsync(int userId, int earnedXp, CancellationToken ct)
    {
        var progress = await _progress.GetByUserIdAsync(userId, ct) ?? new UserProgress { UserId = userId };
        var previousLevel = progress.Level;

        progress.Xp += earnedXp;
        progress.Level = XpCalculator.CalculateLevel((int)progress.Xp);
        progress.UpdatedAt = _time.GetUtcNow();

        await _progress.SaveAsync(progress, ct);

        if (progress.Level > previousLevel)
        {
            _logger.LogInformation("User {UserId} reached level {Level}", userId, progress.Level);
        }
    }
}
