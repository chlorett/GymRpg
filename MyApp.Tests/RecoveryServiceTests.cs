using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using Xunit;

namespace MyApp.Tests;

public sealed class RecoveryServiceTests
{
    private const int AthleteId = 1;
    private const int ChestGroupId = 1;
    private const int BackGroupId = 2;
    private const int LegsGroupId = 3;

    private readonly Mock<IPersonalRecordRepository> _recordRepository = new();
    private readonly Mock<IMuscleLoadRepository> _muscleLoadRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeLogger<RecoveryService> _logger = new();
    private readonly RecoveryService _service;

    public RecoveryServiceTests()
    {
        _userRepository
            .Setup(u => u.GetByIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = AthleteId, Username = "athlete" });

        _muscleLoadRepository
            .Setup(m => m.GetAllMuscleGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MuscleGroup>
            {
                new() { Id = ChestGroupId, Name = "Груди" },
                new() { Id = BackGroupId, Name = "Спина" },
                new() { Id = LegsGroupId, Name = "Ноги" },
            });

        _muscleLoadRepository
            .Setup(m => m.GetAllExerciseMuscleGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ExerciseMuscleGroup>());

        _muscleLoadRepository
            .Setup(m => m.GetUserMuscleLoadsAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MuscleLoad>
            {
                new() { UserId = AthleteId, MuscleGroupId = ChestGroupId, LoadValue = 320m, UpdatedAt = _timeProvider.GetUtcNow() },
                new() { UserId = AthleteId, MuscleGroupId = BackGroupId, LoadValue = 160m, UpdatedAt = _timeProvider.GetUtcNow() },
                new() { UserId = AthleteId, MuscleGroupId = LegsGroupId, LoadValue = 40m, UpdatedAt = _timeProvider.GetUtcNow() },
            });

        _recordRepository
            .Setup(r => r.GetAllByUserIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleWorkoutHistory());

        _service = new RecoveryService(
            _recordRepository.Object,
            _muscleLoadRepository.Object,
            _userRepository.Object,
            _timeProvider,
            _logger);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task GetRecoveryOverviewAsync_WhenUserIdInvalid_LogsWarningAndReturnsValidationFailure(int invalidUserId)
    {
        var result = await _service.GetRecoveryOverviewAsync(invalidUserId, 7);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("userId", result.Field);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    [InlineData(366)]
    public async Task GetRecoveryOverviewAsync_WhenPeriodDaysInvalid_LogsWarningAndReturnsValidationFailure(int invalidPeriod)
    {
        var result = await _service.GetRecoveryOverviewAsync(AthleteId, invalidPeriod);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("periodDays", result.Field);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task GetRecoveryOverviewAsync_CalculatesCurrentAndBestStreaksIgnoringSuspiciousWorkouts()
    {
        var result = await _service.GetRecoveryOverviewAsync(AthleteId, 7);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var overview = result.Value!;

        Assert.Equal(2, overview.CurrentStreakDays);
        Assert.Equal(3, overview.BestStreakDays);
    }

    [Fact]
    public async Task GetRecoveryOverviewAsync_CalculatesPeriodMetricsAndMuscleReadinessStatuses()
    {
        var result = await _service.GetRecoveryOverviewAsync(AthleteId, 7);

        Assert.True(result.IsSuccess);
        var overview = result.Value!;

        Assert.Equal(2, overview.WorkoutsInPeriod);
        Assert.Equal(3, overview.TotalSetsInPeriod);
        Assert.Equal(2600m, overview.TotalVolumeInPeriodKg);
        Assert.Equal(350, overview.EarnedXpInPeriod);
        Assert.Equal(57, overview.ReadinessScorePercent);
        Assert.Equal(3, overview.MuscleRecoveryItems.Count);

        var chest = overview.MuscleRecoveryItems.Single(i => i.MuscleGroupId == ChestGroupId);
        var back = overview.MuscleRecoveryItems.Single(i => i.MuscleGroupId == BackGroupId);
        var legs = overview.MuscleRecoveryItems.Single(i => i.MuscleGroupId == LegsGroupId);

        Assert.Equal(80, chest.FatiguePercent);
        Assert.Equal(20, chest.ReadinessPercent);
        Assert.Equal("Перевантажена", chest.RecoveryStatusText);

        Assert.Equal(40, back.FatiguePercent);
        Assert.Equal(60, back.ReadinessPercent);
        Assert.Equal("Оптимальний тонус", back.RecoveryStatusText);

        Assert.Equal(10, legs.FatiguePercent);
        Assert.Equal(90, legs.ReadinessPercent);
        Assert.Equal("Повністю відновлена", legs.RecoveryStatusText);
    }

    [Fact]
    public void CalculateStreaks_WhenLastWorkoutOlderThanYesterday_ResetsCurrentStreakToZero()
    {
        var oldRecords = new List<WorkoutRecord>
        {
            new()
            {
                Id = 1,
                IsSuspicious = false,
                CreatedAt = new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero),
            },
            new()
            {
                Id = 2,
                IsSuspicious = false,
                CreatedAt = new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero),
            },
        };

        var (current, best) = RecoveryService.CalculateStreaks(oldRecords, new DateOnly(2026, 11, 22));

        Assert.Equal(0, current);
        Assert.Equal(2, best);
    }

    [Theory]
    [InlineData(0, 30, "userId")]
    [InlineData(-2, 30, "userId")]
    [InlineData(1, 5, "reductionPercent")]
    [InlineData(1, 105, "reductionPercent")]
    public async Task ApplyRecoveryDeloadAsync_WhenInputInvalid_LogsWarningAndReturnsValidationFailure(
        int userId,
        int reductionPercent,
        string expectedField)
    {
        var result = await _service.ApplyRecoveryDeloadAsync(userId, reductionPercent);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(expectedField, result.Field);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        _muscleLoadRepository.Verify(
            m => m.SaveUserMuscleLoadsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<MuscleLoad>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyRecoveryDeloadAsync_WhenUserNotFound_LogsWarningAndReturnsUserNotFound()
    {
        _userRepository
            .Setup(u => u.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _service.ApplyRecoveryDeloadAsync(999, 30);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, result.ErrorCode);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task GetRecoveryOverviewAsync_WhenNewWorkoutLoggedAfterStoredLoad_AddsIncrementalFatigue()
    {
        var olderTimestamp = new DateTimeOffset(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);
        _muscleLoadRepository
            .Setup(m => m.GetUserMuscleLoadsAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MuscleLoad>
            {
                new() { UserId = AthleteId, MuscleGroupId = ChestGroupId, LoadValue = 100m, UpdatedAt = olderTimestamp },
                new() { UserId = AthleteId, MuscleGroupId = BackGroupId, LoadValue = 0m, UpdatedAt = olderTimestamp },
                new() { UserId = AthleteId, MuscleGroupId = LegsGroupId, LoadValue = 0m, UpdatedAt = olderTimestamp },
            });

        _muscleLoadRepository
            .Setup(m => m.GetAllExerciseMuscleGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExerciseMuscleGroup>
            {
                new() { ExerciseId = 2, MuscleGroupId = ChestGroupId, LoadFactor = 1.0m },
            });

        var result = await _service.GetRecoveryOverviewAsync(AthleteId, 7);

        Assert.True(result.IsSuccess);
        var chest = result.Value!.MuscleRecoveryItems.Single(i => i.MuscleGroupId == ChestGroupId);
        Assert.Equal(200m, chest.CurrentLoadValue);
        Assert.Equal(50, chest.FatiguePercent);
        Assert.Equal(50, chest.ReadinessPercent);
    }

    [Fact]
    public async Task ApplyRecoveryDeloadAsync_ReducesMuscleLoadSavesToRepositoryAndIncreasesReadiness()
    {
        var result = await _service.ApplyRecoveryDeloadAsync(AthleteId, 50);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var deload = result.Value!;

        Assert.Equal(3, deload.UpdatedGroupsCount);
        Assert.Equal(50, deload.ReductionPercent);
        Assert.Equal(520m, deload.PreviousTotalLoad);
        Assert.Equal(260m, deload.NewTotalLoad);
        Assert.Equal(78, deload.NewReadinessScorePercent);

        _muscleLoadRepository.Verify(
            m => m.SaveUserMuscleLoadsAsync(
                AthleteId,
                It.Is<IReadOnlyList<MuscleLoad>>(loads =>
                    loads.Count == 3 &&
                    loads.Single(l => l.MuscleGroupId == ChestGroupId).LoadValue == 160m &&
                    loads.Single(l => l.MuscleGroupId == BackGroupId).LoadValue == 80m &&
                    loads.Single(l => l.MuscleGroupId == LegsGroupId).LoadValue == 20m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static IReadOnlyList<WorkoutRecord> CreateSampleWorkoutHistory()
    {
        return new List<WorkoutRecord>
        {
            new()
            {
                Id = 1,
                ExerciseId = 1,
                XpEarned = 100,
                IsSuspicious = false,
                CreatedAt = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet> { new() { SetNumber = 1, Weight = 80m, Reps = 10 } },
            },
            new()
            {
                Id = 2,
                ExerciseId = 1,
                XpEarned = 110,
                IsSuspicious = false,
                CreatedAt = new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet> { new() { SetNumber = 1, Weight = 85m, Reps = 10 } },
            },
            new()
            {
                Id = 3,
                ExerciseId = 1,
                XpEarned = 120,
                IsSuspicious = false,
                CreatedAt = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet> { new() { SetNumber = 1, Weight = 90m, Reps = 10 } },
            },
            new()
            {
                Id = 4,
                ExerciseId = 1,
                XpEarned = 0,
                IsSuspicious = true,
                CreatedAt = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet> { new() { SetNumber = 1, Weight = 500m, Reps = 15 } },
            },
            new()
            {
                Id = 5,
                ExerciseId = 1,
                XpEarned = 150,
                IsSuspicious = false,
                CreatedAt = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Weight = 80m, Reps = 10 },
                    new() { SetNumber = 2, Weight = 80m, Reps = 10 },
                },
            },
            new()
            {
                Id = 6,
                ExerciseId = 2,
                XpEarned = 200,
                IsSuspicious = false,
                CreatedAt = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Weight = 100m, Reps = 10 },
                },
            },
        };
    }
}
