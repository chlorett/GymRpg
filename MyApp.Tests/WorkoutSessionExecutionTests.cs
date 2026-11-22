using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Rules;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using Xunit;

namespace MyApp.Tests;

public sealed class WorkoutSessionExecutionTests
{
    private const int UserId = 7;
    private const int SessionId = 5;

    private readonly Mock<IWorkoutSessionRepository> _sessions = new();
    private readonly Mock<IWorkoutTemplateRepository> _templates = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeLogger<WorkoutSessionService> _logger = new();

    // ===== GetSessionExecutionPlanAsync =====
    [Fact]
    public async Task GetPlan_ValidSession_ReturnsExercisesOrderedByPosition()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var service = CreateService();

        var result = await service.GetSessionExecutionPlanAsync(UserId, SessionId);

        Assert.True(result.IsSuccess);
        var plan = result.Value;
        Assert.NotNull(plan);
        Assert.Equal(SessionId, plan.SessionId);
        Assert.Equal("Ноги", plan.WorkoutName);
        Assert.Equal(SessionStatus.Planned, plan.Status);
        Assert.Equal("Заплановано", plan.StatusText);

        Assert.Equal(2, plan.Exercises.Count);
        var first = plan.Exercises[0];
        Assert.Equal(101, first.ExerciseId);
        Assert.Equal("Присідання", first.Name);
        Assert.Equal("Спина рівна", first.Technique);
        Assert.Equal(1, first.Position);
        Assert.Equal(4, first.TargetSets);
        Assert.Equal(8, first.TargetReps);
        Assert.Equal(102, plan.Exercises[1].ExerciseId);
        Assert.Null(plan.Exercises[1].Technique);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPlan_NonPositiveSessionId_ReturnsSessionNotFound(int sessionId)
    {
        var service = CreateService();

        var result = await service.GetSessionExecutionPlanAsync(UserId, sessionId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionNotFound, result.ErrorCode);
        Assert.Null(result.Value);
        _sessions.Verify(
            r => r.GetSessionWithExercisesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPlan_SessionNotFound_ReturnsSessionNotFound()
    {
        _sessions
            .Setup(r => r.GetSessionWithExercisesAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkoutSession?)null);
        var service = CreateService();

        var result = await service.GetSessionExecutionPlanAsync(UserId, SessionId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetPlan_ForeignSession_ReturnsSessionAccessDenied()
    {
        SetupSession(SessionId, 999, SessionStatus.Planned);
        var service = CreateService();

        var result = await service.GetSessionExecutionPlanAsync(UserId, SessionId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionAccessDenied, result.ErrorCode);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Canceled)]
    [InlineData(SessionStatus.Skipped)]
    public async Task GetPlan_NotPlannedSession_ReturnsInvalidTransition(SessionStatus status)
    {
        SetupSession(SessionId, UserId, status);
        var service = CreateService();

        var result = await service.GetSessionExecutionPlanAsync(UserId, SessionId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidSessionStatusTransition, result.ErrorCode);
    }

    [Fact]
    public async Task GetPlan_AccessFailure_LogsWarning()
    {
        SetupSession(SessionId, 999, SessionStatus.Planned);
        var service = CreateService();

        await service.GetSessionExecutionPlanAsync(UserId, SessionId);

        Assert.Equal(LogLevel.Warning, _logger.LatestRecord.Level);
    }

    // ===== CompleteSessionWithRecordsAsync: помилки доступу та статусу =====
    [Fact]
    public async Task Complete_NullRequest_ReturnsValidationFailed()
    {
        var service = CreateService();

        var result = await service.CompleteSessionWithRecordsAsync(null!);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Complete_NonPositiveSessionId_ReturnsSessionNotFound(int sessionId)
    {
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(UserId, sessionId, new[] { Executed(101, (10, 50m)) });

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionNotFound, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Fact]
    public async Task Complete_SessionNotFound_ReturnsSessionNotFound()
    {
        _sessions
            .Setup(r => r.GetSessionWithExercisesAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkoutSession?)null);
        var service = CreateService();

        var result = await service.CompleteSessionWithRecordsAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionNotFound, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Fact]
    public async Task Complete_ForeignSession_ReturnsSessionAccessDenied()
    {
        SetupSession(SessionId, 999, SessionStatus.Planned);
        var service = CreateService();

        var result = await service.CompleteSessionWithRecordsAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionAccessDenied, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Theory]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Canceled)]
    [InlineData(SessionStatus.Skipped)]
    public async Task Complete_NotPlannedSession_ReturnsInvalidTransition(SessionStatus status)
    {
        SetupSession(SessionId, UserId, status);
        var service = CreateService();

        var result = await service.CompleteSessionWithRecordsAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidSessionStatusTransition, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Fact]
    public async Task Complete_AccessFailure_LogsWarning()
    {
        SetupSession(SessionId, 999, SessionStatus.Planned);
        var service = CreateService();

        await service.CompleteSessionWithRecordsAsync(ValidRequest());

        Assert.Equal(LogLevel.Warning, _logger.LatestRecord.Level);
    }

    // ===== CompleteSessionWithRecordsAsync: валідація підходів (A3) =====
    [Fact]
    public async Task Complete_EmptyExerciseList_ReturnsValidationFailed()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(UserId, SessionId, Array.Empty<ExecutedExerciseDto>());

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Fact]
    public async Task Complete_AllExercisesWithoutSets_ReturnsValidationFailed()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(
            UserId,
            SessionId,
            new[] { Executed(101), Executed(102) });

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Fact]
    public async Task Complete_ExerciseNotInPlan_ReturnsValidationFailed()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(UserId, SessionId, new[] { Executed(999, (10, 50m)) });

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Fact]
    public async Task Complete_DuplicateExercise_ReturnsValidationFailed()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(
            UserId,
            SessionId,
            new[] { Executed(101, (10, 50m)), Executed(101, (8, 50m)) });

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(WorkoutRules.MaxReps + 1)]
    public async Task Complete_RepsOutOfRange_ReturnsValidationFailed(int reps)
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(UserId, SessionId, new[] { Executed(101, (reps, 50m)) });

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task Complete_WeightOutOfRange_ReturnsValidationFailed(int weight)
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(UserId, SessionId, new[] { Executed(101, (10, weight)) });

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Fact]
    public async Task Complete_TooManySets_ReturnsValidationFailed()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var service = CreateService();
        var sets = Enumerable.Repeat((10, 50m), WorkoutRules.MaxSets + 1).ToArray();
        var request = new CompleteSessionExecutionRequest(UserId, SessionId, new[] { Executed(101, sets) });

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        VerifyNotCompleted();
    }

    [Fact]
    public async Task Complete_ValidationFailure_LogsWarning()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(UserId, SessionId, new[] { Executed(101, (0, 50m)) });

        await service.CompleteSessionWithRecordsAsync(request);

        Assert.Equal(LogLevel.Warning, _logger.LatestRecord.Level);
    }

    // ===== CompleteSessionWithRecordsAsync: успішні сценарії =====
    [Fact]
    public async Task Complete_ValidRequest_CreatesRecordsWithSetsAndReturnsSummary()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var captured = CaptureCompletion(new SessionCompletionOutcome(2500, 3));
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(
            UserId,
            SessionId,
            new[] { Executed(101, (10, 50m), (8, 50m)), Executed(102) });

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.True(result.IsSuccess);
        var summary = result.Value;
        Assert.NotNull(summary);
        Assert.Equal(SessionId, summary.SessionId);
        Assert.Equal(900, summary.EarnedXp); // 10*50 + 8*50
        Assert.Equal(3, summary.NewLevel);
        Assert.Equal(1, summary.CompletedExercisesCount);

        Assert.NotNull(captured.Records);
        var record = Assert.Single(captured.Records);
        Assert.Equal(SessionId, record.SessionId);
        Assert.Equal(101, record.ExerciseId);
        Assert.Equal(900, record.XpEarned);
        Assert.False(record.IsSuspicious);

        var sets = record.WorkoutSets.ToList();
        Assert.Equal(2, sets.Count);
        Assert.Equal(1, sets[0].SetNumber);
        Assert.Equal(10, sets[0].Reps);
        Assert.Equal(50m, sets[0].Weight);
        Assert.Equal(2, sets[1].SetNumber);
        Assert.Equal(8, sets[1].Reps);
    }

    [Fact]
    public async Task Complete_ValidRequest_PassesUserIdAndCurrentUtcTimeToRepository()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var captured = CaptureCompletion(new SessionCompletionOutcome(900, 1));
        var service = CreateService();

        await service.CompleteSessionWithRecordsAsync(ValidRequest());

        Assert.Equal(UserId, captured.CapturedUserId);
        Assert.Equal(_time.GetUtcNow(), captured.CompletedAt);
    }

    [Fact]
    public async Task Complete_ExercisesWithoutSets_AreSkipped()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var captured = CaptureCompletion(new SessionCompletionOutcome(500, 1));
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(
            UserId,
            SessionId,
            new[] { Executed(101), Executed(102, (10, 50m)) });

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.CompletedExercisesCount);
        Assert.NotNull(captured.Records);
        Assert.Equal(102, Assert.Single(captured.Records).ExerciseId);
    }

    [Fact]
    public async Task Complete_BodyweightSet_UsesDefaultWeightForXp()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        CaptureCompletion(new SessionCompletionOutcome(80, 1));
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(UserId, SessionId, new[] { Executed(101, (8, 0m)) });

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(80, result.Value!.EarnedXp); // 8 * 10 (вага за замовчуванням)
    }

    [Fact]
    public async Task Complete_SuspiciousWeight_FlagsRecord()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        var captured = CaptureCompletion(new SessionCompletionOutcome(4500, 5));
        var service = CreateService();
        var request = new CompleteSessionExecutionRequest(UserId, SessionId, new[] { Executed(101, (10, 450m)) });

        var result = await service.CompleteSessionWithRecordsAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured.Records);
        Assert.True(Assert.Single(captured.Records).IsSuspicious);
    }

    [Fact]
    public async Task Complete_RepositoryReportsSessionNoLongerPlanned_ReturnsInvalidTransition()
    {
        SetupSession(SessionId, UserId, SessionStatus.Planned);
        _sessions
            .Setup(r => r.CompleteSessionWithRecordsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<WorkoutRecord>>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionCompletionOutcome?)null);
        var service = CreateService();

        var result = await service.CompleteSessionWithRecordsAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidSessionStatusTransition, result.ErrorCode);
        Assert.Null(result.Value);
        Assert.Equal(LogLevel.Warning, _logger.LatestRecord.Level);
    }

    private static ExecutedExerciseDto Executed(int exerciseId, params (int Reps, decimal Weight)[] sets) =>
        new(exerciseId, sets.Select(s => new WorkoutSetDto(s.Reps, s.Weight)).ToList());

    private static CompleteSessionExecutionRequest ValidRequest() =>
        new(UserId, SessionId, new[] { Executed(101, (10, 50m), (8, 50m)) });

    private WorkoutSessionService CreateService() =>
        new(_sessions.Object, _templates.Object, _time, _logger);

    private void VerifyNotCompleted() =>
        _sessions.Verify(
            r => r.CompleteSessionWithRecordsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<WorkoutRecord>>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

    private CompletionCapture CaptureCompletion(SessionCompletionOutcome outcome)
    {
        var capture = new CompletionCapture();
        _sessions
            .Setup(r => r.CompleteSessionWithRecordsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<WorkoutRecord>>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, int, IReadOnlyList<WorkoutRecord>, DateTimeOffset, CancellationToken>(
                (_, userId, records, completedAt, _) =>
                {
                    capture.CapturedUserId = userId;
                    capture.Records = records;
                    capture.CompletedAt = completedAt;
                })
            .ReturnsAsync(outcome);
        return capture;
    }

    // Навмисно у зворотному порядку Position, щоб перевірити сортування в плані.
    private void SetupSession(int sessionId, long ownerId, SessionStatus status) =>
        _sessions
            .Setup(r => r.GetSessionWithExercisesAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkoutSession
            {
                Id = sessionId,
                Status = status,
                ScheduledDate = new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc),
                Workout = new Workout
                {
                    Id = 1,
                    UserId = ownerId,
                    Name = "Ноги",
                    WorkoutExercises = new List<WorkoutExercise>
                    {
                        new()
                        {
                            ExerciseId = 102,
                            Position = 2,
                            TargetSets = 3,
                            TargetReps = 12,
                            Exercise = new Exercise { Id = 102, Name = "Жим ногами", Technique = null },
                        },
                        new()
                        {
                            ExerciseId = 101,
                            Position = 1,
                            TargetSets = 4,
                            TargetReps = 8,
                            Exercise = new Exercise { Id = 101, Name = "Присідання", Technique = "Спина рівна" },
                        },
                    },
                },
            });

    private sealed class CompletionCapture
    {
        public int CapturedUserId { get; set; }

        public IReadOnlyList<WorkoutRecord>? Records { get; set; }

        public DateTimeOffset CompletedAt { get; set; }
    }
}