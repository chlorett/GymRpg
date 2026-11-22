using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Rules;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public sealed partial class WorkoutSessionService
{
    public async Task<OperationResult<SessionExecutionPlanDto>> GetSessionExecutionPlanAsync(
        int userId,
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId <= 0)
        {
            return FailExecution<SessionExecutionPlanDto>(
                ErrorCodes.SessionNotFound, nameof(sessionId), "Сесію не знайдено.");
        }

        var session = await _sessionRepository.GetSessionWithExercisesAsync(sessionId, cancellationToken);

        var accessError = CheckExecutionAccess(session, userId);
        if (accessError is not null)
        {
            return FailExecution<SessionExecutionPlanDto>(
                accessError.Value.ErrorCode, accessError.Value.Field, accessError.Value.Message);
        }

        var exercises = session!.Workout.WorkoutExercises
            .OrderBy(e => e.Position)
            .Select(e => new SessionExecutionExerciseDto(
                e.ExerciseId,
                e.Exercise.Name,
                e.Exercise.Technique,
                e.Position,
                e.TargetSets,
                e.TargetReps))
            .ToList();

        var dto = new SessionExecutionPlanDto(
            (int)session.Id,
            session.Workout.Name,
            session.ScheduledDate,
            session.Status,
            ToStatusText(session.Status),
            exercises);

        return OperationResult.Success(dto);
    }

    public async Task<OperationResult<SessionExecutionSummaryDto>> CompleteSessionWithRecordsAsync(
        CompleteSessionExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return FailExecution<SessionExecutionSummaryDto>(
                ErrorCodes.ValidationFailed, nameof(request), "Запит не може бути порожнім.");
        }

        if (request.SessionId <= 0)
        {
            return FailExecution<SessionExecutionSummaryDto>(
                ErrorCodes.SessionNotFound, nameof(request.SessionId), "Сесію не знайдено.");
        }

        var session = await _sessionRepository.GetSessionWithExercisesAsync(request.SessionId, cancellationToken);

        var accessError = CheckExecutionAccess(session, request.UserId);
        if (accessError is not null)
        {
            return FailExecution<SessionExecutionSummaryDto>(
                accessError.Value.ErrorCode, accessError.Value.Field, accessError.Value.Message);
        }

        if (request.Exercises is null || request.Exercises.Count == 0)
        {
            return FailExecution<SessionExecutionSummaryDto>(
                ErrorCodes.ValidationFailed, nameof(request.Exercises), "Вкажіть виконані вправи.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        var planned = session!.Workout.WorkoutExercises.ToDictionary(e => e.ExerciseId);
        var seen = new HashSet<int>();
        var records = new List<WorkoutRecord>();

        foreach (var executed in request.Exercises)
        {
            if (!planned.ContainsKey(executed.ExerciseId))
            {
                return FailExecution<SessionExecutionSummaryDto>(
                    ErrorCodes.ValidationFailed,
                    nameof(request.Exercises),
                    "Вправа не входить до плану цієї сесії.");
            }

            if (!seen.Add(executed.ExerciseId))
            {
                return FailExecution<SessionExecutionSummaryDto>(
                    ErrorCodes.ValidationFailed,
                    nameof(request.Exercises),
                    "Вправа вказана більше одного разу.");
            }

            var sets = executed.Sets;
            if (sets is null || sets.Count == 0)
            {
                continue; // вправу не виконували
            }

            if (sets.Count > WorkoutRules.MaxSets)
            {
                return FailExecution<SessionExecutionSummaryDto>(
                    ErrorCodes.ValidationFailed,
                    nameof(executed.Sets),
                    $"Кількість підходів не може перевищувати {WorkoutRules.MaxSets}.");
            }

            foreach (var set in sets)
            {
                if (set.Reps < WorkoutRules.MinReps || set.Reps > WorkoutRules.MaxReps)
                {
                    return FailExecution<SessionExecutionSummaryDto>(
                        ErrorCodes.ValidationFailed,
                        nameof(set.Reps),
                        $"Кількість повторень має бути від {WorkoutRules.MinReps} до {WorkoutRules.MaxReps}.");
                }

                if (set.Weight < WorkoutRules.MinWeight || set.Weight > WorkoutRules.MaxWeight)
                {
                    return FailExecution<SessionExecutionSummaryDto>(
                        ErrorCodes.ValidationFailed,
                        nameof(set.Weight),
                        $"Вага має бути від {WorkoutRules.MinWeight} до {WorkoutRules.MaxWeight} кг.");
                }
            }

            records.Add(new WorkoutRecord
            {
                SessionId = session.Id,
                ExerciseId = executed.ExerciseId,
                XpEarned = XpCalculator.CalculateXp(sets),
                IsSuspicious = XpCalculator.IsSuspicious(sets),
                CreatedAt = now,
                WorkoutSets = sets
                    .Select((s, i) => new WorkoutSet
                    {
                        SetNumber = (short)(i + 1),
                        Reps = (short)s.Reps,
                        Weight = s.Weight,
                    })
                    .ToList(),
            });
        }

        if (records.Count == 0)
        {
            return FailExecution<SessionExecutionSummaryDto>(
                ErrorCodes.ValidationFailed,
                nameof(request.Exercises),
                "Потрібно вказати хоча б один виконаний підхід.");
        }

        var outcome = await _sessionRepository.CompleteSessionWithRecordsAsync(
            request.SessionId, request.UserId, records, now, cancellationToken);

        if (outcome is null)
        {
            return FailExecution<SessionExecutionSummaryDto>(
                ErrorCodes.InvalidSessionStatusTransition,
                nameof(request.SessionId),
                "Сесію вже завершено або скасовано.");
        }

        int earnedXp = records.Sum(r => r.XpEarned);

        _logger.LogInformation(
            "Сесію {SessionId} завершено користувачем {UserId}: XP={EarnedXp}, рівень={Level}",
            request.SessionId,
            request.UserId,
            earnedXp,
            outcome.Level);

        return OperationResult.Success(new SessionExecutionSummaryDto(
            request.SessionId, earnedXp, outcome.Level, records.Count));
    }

    // A1 + A2: сесії немає / чужа / не в статусі Planned.
    private static (string ErrorCode, string Field, string Message)? CheckExecutionAccess(
        WorkoutSession? session, int userId)
    {
        if (session is null)
        {
            return (ErrorCodes.SessionNotFound, "sessionId", "Сесію не знайдено.");
        }

        if (userId <= 0 || session.Workout.UserId != userId)
        {
            return (ErrorCodes.SessionAccessDenied, "userId", "Ви не маєте доступу до цієї сесії.");
        }

        if (session.Status != SessionStatus.Planned)
        {
            return (ErrorCodes.InvalidSessionStatusTransition, "status",
                "Провести можна лише заплановану сесію.");
        }

        return null;
    }

    private OperationResult<T> FailExecution<T>(string errorCode, string field, string message)
    {
        _logger.LogWarning(
            "Відмова при проведенні сесії: [{ErrorCode}] Field={Field} Message={Message}",
            errorCode,
            field,
            message);

        return OperationResult.Fail<T>(errorCode, field, message);
    }
}