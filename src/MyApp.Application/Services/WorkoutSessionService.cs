using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public sealed class WorkoutSessionService : IWorkoutSessionService
{
    private const int MaxPastDays = 30;
    private const int MaxNameLength = 100;

    private readonly IWorkoutSessionRepository _sessionRepository;
    private readonly IWorkoutTemplateRepository _templateRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WorkoutSessionService> _logger;

    public WorkoutSessionService(
        IWorkoutSessionRepository sessionRepository,
        IWorkoutTemplateRepository templateRepository,
        TimeProvider timeProvider,
        ILogger<WorkoutSessionService> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<WorkoutSessionItemDto>> GetUserSessionsAsync(
        int userId,
        SessionStatus? statusFilter,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Array.Empty<WorkoutSessionItemDto>();
        }

        var sessions = await _sessionRepository.GetSessionsByUserIdAsync(userId, cancellationToken);

        return sessions
            .Where(s => statusFilter is null || s.Status == statusFilter)
            .Select(ToDto)
            .ToList();
    }

    public async Task<OperationResult> ScheduleSessionFromTemplateAsync(
        ScheduleWorkoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(request), "Запит не може бути порожнім.");
        }

        if (request.UserId <= 0)
        {
            return Fail(ErrorCodes.UserNotFound, nameof(request.UserId), "Невідомий користувач.");
        }

        string name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(request.Name),
                $"Вкажіть назву тренування (до {MaxNameLength} символів).");
        }

        DateTime today = _timeProvider.GetLocalNow().Date;
        if (request.ScheduledDate.Date < today.AddDays(-MaxPastDays))
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(request.ScheduledDate),
                "Дата тренування занадто далеко в минулому.");
        }

        var template = await _templateRepository.GetByIdWithExercisesAsync(request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Fail(
                ErrorCodes.TemplateNotFound,
                nameof(request.TemplateId),
                "Шаблон тренування не знайдено.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        var workout = new Workout
        {
            UserId = request.UserId,
            Name = name,
            BasedOnTemplateId = template.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var exercise in template.WorkoutTemplateExercises.OrderBy(e => e.Position))
        {
            workout.WorkoutExercises.Add(new WorkoutExercise
            {
                ExerciseId = exercise.ExerciseId,
                Position = exercise.Position,
                TargetSets = exercise.TargetSets,
                TargetReps = exercise.TargetReps,
            });
        }

        var session = new WorkoutSession
        {
            ScheduledDate = DateTime.SpecifyKind(request.ScheduledDate.Date, DateTimeKind.Utc),
            Status = SessionStatus.Planned,
            CreatedAt = now,
        };

        await _sessionRepository.AddWorkoutWithSessionAsync(workout, session, cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> UpdateSessionStatusAsync(
        UpdateSessionStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(request), "Запит не може бути порожнім.");
        }

        var session = await _sessionRepository.GetSessionByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
        {
            return Fail(ErrorCodes.SessionNotFound, nameof(request.SessionId), "Сесію не знайдено.");
        }

        if (session.Workout.UserId != request.UserId)
        {
            return Fail(
                ErrorCodes.SessionAccessDenied,
                nameof(request.UserId),
                "Ви не маєте доступу до цієї сесії.");
        }

        if (!IsTransitionAllowed(session.Status, request.NewStatus))
        {
            return Fail(
                ErrorCodes.InvalidSessionStatusTransition,
                nameof(request.NewStatus),
                "Неможливо змінити статус сесії на обраний.");
        }

        DateTimeOffset? completedAt = request.NewStatus == SessionStatus.Completed
            ? _timeProvider.GetUtcNow()
            : null;

        bool updated = await _sessionRepository.UpdateSessionStatusAsync(
            request.SessionId,
            request.NewStatus,
            completedAt,
            cancellationToken);

        return updated
            ? OperationResult.Success()
            : Fail(ErrorCodes.SessionNotFound, nameof(request.SessionId), "Сесію не знайдено.");
    }

    // Дозволено лише зі стану Planned у будь-який інший статус.
    private static bool IsTransitionAllowed(SessionStatus from, SessionStatus to) =>
        from != to;

    private static string ToStatusText(SessionStatus status) => status switch
    {
        SessionStatus.Planned => "Заплановано",
        SessionStatus.Completed => "Виконано",
        SessionStatus.Skipped => "Пропущено",
        SessionStatus.Canceled => "Скасовано",
        _ => status.ToString(),
    };

    private static WorkoutSessionItemDto ToDto(WorkoutSession session) =>
        new(
            (int)session.Id,
            (int)session.WorkoutId,
            session.Workout.Name,
            session.ScheduledDate,
            session.Status,
            ToStatusText(session.Status),
            session.CompletedAt,
            session.Workout.WorkoutExercises.Count);

    private OperationResult Fail(string errorCode, string field, string message)
    {
        _logger.LogWarning(
            "Відмова валідації розкладу тренувань: [{ErrorCode}] Field={Field} Message={Message}",
            errorCode,
            field,
            message);

        return OperationResult.Fail(errorCode, field, message);
    }
}