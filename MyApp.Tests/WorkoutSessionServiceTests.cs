using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using Xunit;

namespace MyApp.Tests;

public sealed class WorkoutSessionServiceTests
{
    private const int UserId = 7;
    private const int TemplateId = 3;

    private readonly Mock<IWorkoutSessionRepository> _sessions = new();
    private readonly Mock<IWorkoutTemplateRepository> _templates = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeLogger<WorkoutSessionService> _logger = new();

    [Fact]
    public async Task Schedule_NullRequest_ReturnsValidationFailed()
    {
        var service = CreateService();

        var result = await service.ScheduleSessionFromTemplateAsync(null!);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Schedule_EmptyName_ReturnsValidationFailedOnNameField()
    {
        var service = CreateService();
        var request = new ScheduleWorkoutSessionRequest(UserId, TemplateId, "   ", _time.GetLocalNow().Date);

        var result = await service.ScheduleSessionFromTemplateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(ScheduleWorkoutSessionRequest.Name), result.Field);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Schedule_DateFarInThePast_ReturnsValidationFailedOnDateField()
    {
        var service = CreateService();
        var request = new ScheduleWorkoutSessionRequest(
            UserId,
            TemplateId,
            "Ноги",
            _time.GetLocalNow().Date.AddDays(-60));

        var result = await service.ScheduleSessionFromTemplateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(ScheduleWorkoutSessionRequest.ScheduledDate), result.Field);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Schedule_TemplateNotFound_ReturnsTemplateNotFound()
    {
        _templates
            .Setup(r => r.GetByIdWithExercisesAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkoutTemplate?)null);
        var service = CreateService();
        var request = new ScheduleWorkoutSessionRequest(UserId, TemplateId, "Ноги", _time.GetLocalNow().Date);

        var result = await service.ScheduleSessionFromTemplateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.TemplateNotFound, result.ErrorCode);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Schedule_ValidationFailure_LogsWarning()
    {
        var service = CreateService();
        var request = new ScheduleWorkoutSessionRequest(UserId, TemplateId, string.Empty, _time.GetLocalNow().Date);

        await service.ScheduleSessionFromTemplateAsync(request);

        Assert.Equal(LogLevel.Warning, _logger.LatestRecord.Level);
    }

    [Fact]
    public async Task Schedule_ValidRequest_CopiesExercisesAndCreatesPlannedSession()
    {
        _templates
            .Setup(r => r.GetByIdWithExercisesAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTemplate());

        Workout? savedWorkout = null;
        WorkoutSession? savedSession = null;
        _sessions
            .Setup(r => r.AddWorkoutWithSessionAsync(
                It.IsAny<Workout>(),
                It.IsAny<WorkoutSession>(),
                It.IsAny<CancellationToken>()))
            .Callback<Workout, WorkoutSession, CancellationToken>((workout, session, _) =>
            {
                savedWorkout = workout;
                savedSession = session;
            })
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new ScheduleWorkoutSessionRequest(
            UserId,
            TemplateId,
            "  Ноги  ",
            new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Local));

        var result = await service.ScheduleSessionFromTemplateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(savedWorkout);
        Assert.NotNull(savedSession);

        Assert.Equal(UserId, savedWorkout.UserId);
        Assert.Equal("Ноги", savedWorkout.Name);
        Assert.Equal(TemplateId, savedWorkout.BasedOnTemplateId);

        Assert.Equal(2, savedWorkout.WorkoutExercises.Count);
        var first = savedWorkout.WorkoutExercises.First();
        Assert.Equal(101, first.ExerciseId);
        Assert.Equal(1, first.Position);
        Assert.Equal(4, first.TargetSets);
        Assert.Equal(8, first.TargetReps);

        Assert.Equal(SessionStatus.Planned, savedSession.Status);
        Assert.Null(savedSession.CompletedAt);
    }

    [Fact]
    public async Task Schedule_LocalKindDate_IsSavedAsUtcSameDay()
    {
        _templates
            .Setup(r => r.GetByIdWithExercisesAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTemplate());

        WorkoutSession? savedSession = null;
        _sessions
            .Setup(r => r.AddWorkoutWithSessionAsync(
                It.IsAny<Workout>(),
                It.IsAny<WorkoutSession>(),
                It.IsAny<CancellationToken>()))
            .Callback<Workout, WorkoutSession, CancellationToken>((_, session, _) => savedSession = session)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new ScheduleWorkoutSessionRequest(
            UserId,
            TemplateId,
            "Ноги",
            new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Local));

        await service.ScheduleSessionFromTemplateAsync(request);

        Assert.NotNull(savedSession);
        Assert.Equal(DateTimeKind.Utc, savedSession.ScheduledDate.Kind);
        Assert.Equal(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc), savedSession.ScheduledDate.Date);
    }

    [Fact]
    public async Task UpdateStatus_PlannedToCompleted_SetsCompletedAt()
    {
        SetupSession(sessionId: 5, ownerId: UserId, status: SessionStatus.Planned);
        _sessions
            .Setup(r => r.UpdateSessionStatusAsync(
                5,
                SessionStatus.Completed,
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();

        var result = await service.UpdateSessionStatusAsync(
            new UpdateSessionStatusRequest(UserId, 5, SessionStatus.Completed));

        Assert.True(result.IsSuccess);
        _sessions.Verify(
            r => r.UpdateSessionStatusAsync(
                5,
                SessionStatus.Completed,
                It.Is<DateTimeOffset?>(d => d == _time.GetUtcNow()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(SessionStatus.Skipped)]
    [InlineData(SessionStatus.Canceled)]
    public async Task UpdateStatus_PlannedToSkippedOrCanceled_DoesNotSetCompletedAt(SessionStatus newStatus)
    {
        SetupSession(sessionId: 5, ownerId: UserId, status: SessionStatus.Planned);
        _sessions
            .Setup(r => r.UpdateSessionStatusAsync(
                5,
                newStatus,
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();

        var result = await service.UpdateSessionStatusAsync(
            new UpdateSessionStatusRequest(UserId, 5, newStatus));

        Assert.True(result.IsSuccess);
        _sessions.Verify(
            r => r.UpdateSessionStatusAsync(
                5,
                newStatus,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_ForeignSession_ReturnsSessionAccessDenied()
    {
        SetupSession(sessionId: 5, ownerId: 999, status: SessionStatus.Planned);
        var service = CreateService();

        var result = await service.UpdateSessionStatusAsync(
            new UpdateSessionStatusRequest(UserId, 5, SessionStatus.Completed));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionAccessDenied, result.ErrorCode);
        VerifyStatusNotUpdated();
    }

    [Fact]
    public async Task UpdateStatus_SessionNotFound_ReturnsSessionNotFound()
    {
        _sessions
            .Setup(r => r.GetSessionByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkoutSession?)null);
        var service = CreateService();

        var result = await service.UpdateSessionStatusAsync(
            new UpdateSessionStatusRequest(UserId, 5, SessionStatus.Completed));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionNotFound, result.ErrorCode);
        VerifyStatusNotUpdated();
    }

    [Theory]
    [InlineData(SessionStatus.Planned)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Skipped)]
    [InlineData(SessionStatus.Canceled)]
    public async Task UpdateStatus_SameStatus_ReturnsInvalidTransition(SessionStatus currentStatus)
    {
        SetupSession(sessionId: 5, ownerId: UserId, status: currentStatus);
        var service = CreateService();

        var result = await service.UpdateSessionStatusAsync(
            new UpdateSessionStatusRequest(UserId, 5, currentStatus));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidSessionStatusTransition, result.ErrorCode);
        VerifyStatusNotUpdated();
    }

    [Fact]
    public async Task GetUserSessions_WithStatusFilter_ReturnsOnlyMatchingSessions()
    {
        SetupUserSessions();
        var service = CreateService();

        var result = await service.GetUserSessionsAsync(UserId, SessionStatus.Planned);

        var item = Assert.Single(result);
        Assert.Equal(SessionStatus.Planned, item.Status);
        Assert.Equal("Заплановано", item.StatusText);
    }

    [Fact]
    public async Task GetUserSessions_WithoutFilter_ReturnsAllSessions()
    {
        SetupUserSessions();
        var service = CreateService();

        var result = await service.GetUserSessionsAsync(UserId, null);

        Assert.Equal(3, result.Count);
    }

    private static WorkoutSession CreateSession(long id, SessionStatus status) =>
    new()
    {
        Id = id,
        WorkoutId = id,
        Status = status,
        ScheduledDate = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc),
        Workout = new Workout { Id = id, UserId = UserId, Name = $"Тренування {id}" },
    };

    private static WorkoutTemplate CreateTemplate() =>
        new()
        {
            Id = TemplateId,
            Name = "Ноги",
            WorkoutTemplateExercises = new List<WorkoutTemplateExercise>
            {
                // Навмисно у зворотному порядку, щоб перевірити сортування за Position.
                new() { ExerciseId = 102, Position = 2, TargetSets = 3, TargetReps = 12 },
                new() { ExerciseId = 101, Position = 1, TargetSets = 4, TargetReps = 8 },
            },
        };

    private WorkoutSessionService CreateService() =>
        new(_sessions.Object, _templates.Object, _time, _logger);

    private void VerifyNothingSaved() =>
        _sessions.Verify(
            r => r.AddWorkoutWithSessionAsync(
                It.IsAny<Workout>(),
                It.IsAny<WorkoutSession>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

    private void VerifyStatusNotUpdated() =>
        _sessions.Verify(
            r => r.UpdateSessionStatusAsync(
                It.IsAny<int>(),
                It.IsAny<SessionStatus>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

    private void SetupSession(int sessionId, long ownerId, SessionStatus status) =>
        _sessions
            .Setup(r => r.GetSessionByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkoutSession
            {
                Id = sessionId,
                Status = status,
                Workout = new Workout { Id = 1, UserId = ownerId, Name = "Ноги" },
            });

    private void SetupUserSessions() =>
        _sessions
            .Setup(r => r.GetSessionsByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkoutSession>
            {
                CreateSession(1, SessionStatus.Planned),
                CreateSession(2, SessionStatus.Completed),
                CreateSession(3, SessionStatus.Canceled),
            });
}