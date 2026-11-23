using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using Xunit;

namespace MyApp.Tests;

public sealed class StrengthStandardsServiceTests
{
    private const int AthleteId = 1;
    private const int BenchPressId = 10;
    private const int SquatId = 20;
    private const int CurlId = 30;

    private readonly Mock<IPersonalRecordRepository> _recordRepository = new();
    private readonly Mock<IMuscleLoadRepository> _muscleLoadRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IExerciseRepository> _exerciseRepository = new();
    private readonly FakeLogger<StrengthStandardsService> _logger = new();
    private readonly StrengthStandardsService _service;

    public StrengthStandardsServiceTests()
    {
        _userRepository
            .Setup(u => u.GetByIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = AthleteId, Username = "athlete", WeightKg = 80.0m });

        _exerciseRepository
            .Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Exercise>
            {
                new() { Id = BenchPressId, Name = "Жим штанги лежачи" },
                new() { Id = SquatId, Name = "Присідання зі штангою" },
                new() { Id = CurlId, Name = "Підйом на біцепс" },
            });

        _exerciseRepository
            .Setup(e => e.GetByIdAsync(BenchPressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Exercise { Id = BenchPressId, Name = "Жим штанги лежачи" });

        _muscleLoadRepository
            .Setup(m => m.GetAllMuscleGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MuscleGroup>
            {
                new() { Id = 1, Name = "Груди" },
                new() { Id = 2, Name = "Спина" },
                new() { Id = 3, Name = "Ноги" },
            });

        _muscleLoadRepository
            .Setup(m => m.GetAllExerciseMuscleGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ExerciseMuscleGroup>());

        _muscleLoadRepository
            .Setup(m => m.GetUserMuscleLoadsAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MuscleLoad>
            {
                new() { UserId = AthleteId, MuscleGroupId = 1, LoadValue = 100m, UpdatedAt = DateTimeOffset.UtcNow },
                new() { UserId = AthleteId, MuscleGroupId = 2, LoadValue = 100m, UpdatedAt = DateTimeOffset.UtcNow },
                new() { UserId = AthleteId, MuscleGroupId = 3, LoadValue = 100m, UpdatedAt = DateTimeOffset.UtcNow },
            });

        _recordRepository
            .Setup(r => r.GetAllByUserIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleRecords());

        _service = new StrengthStandardsService(
            _recordRepository.Object,
            _muscleLoadRepository.Object,
            _userRepository.Object,
            _exerciseRepository.Object,
            _logger);
    }

    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(100, 0, 0)]
    [InlineData(100, 1, 100)]
    [InlineData(120, 10, 90)]
    [InlineData(140, 5, 120)]
    public void CalculateWorkingWeightForTarget1Rm_ReturnsExpectedInverseEpleyValue(
        decimal target1RmKg,
        int targetReps,
        decimal expectedWeightKg)
    {
        decimal actual = StrengthStandardsService.CalculateWorkingWeightForTarget1Rm(target1RmKg, targetReps);

        Assert.Equal(expectedWeightKg, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetStrengthStandardsOverviewAsync_WhenUserIdInvalid_LogsWarningAndReturnsValidationFailure(int invalidUserId)
    {
        var result = await _service.GetStrengthStandardsOverviewAsync(invalidUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("userId", result.Field);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task GetStrengthStandardsOverviewAsync_WhenRankFilterInvalid_LogsWarningAndReturnsValidationFailure()
    {
        var result = await _service.GetStrengthStandardsOverviewAsync(AthleteId, "НевідомийРозряд");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("rankFilter", result.Field);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task GetStrengthStandardsOverviewAsync_CalculatesRelativeRatiosRanksAndIgnoresSuspiciousWorkouts()
    {
        var result = await _service.GetStrengthStandardsOverviewAsync(AthleteId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var overview = result.Value!;

        Assert.Equal(80.0m, overview.AthleteBodyWeightKg);
        Assert.Equal(1.75m, overview.BestRelativeRatio);
        Assert.Equal("Еліта", overview.OverallStrengthRank);
        Assert.Equal(2, overview.ExerciseStandards.Count);

        var squat = overview.ExerciseStandards.Single(s => s.ExerciseId == SquatId);
        Assert.Equal(140m, squat.BestOneRepMaxKg);
        Assert.Equal(1.75m, squat.RelativeStrengthRatio);
        Assert.Equal("Еліта", squat.StrengthRankTitle);

        var bench = overview.ExerciseStandards.Single(s => s.ExerciseId == BenchPressId);
        Assert.Equal(90m, bench.BestOneRepMaxKg);
        Assert.Equal(1.12m, bench.RelativeStrengthRatio);
        Assert.Equal("Любитель", bench.StrengthRankTitle);
        Assert.Equal("Атлет", bench.NextRankTitle);
        Assert.Equal(100m, bench.TargetNextRank1RmKg);
        Assert.Equal(10m, bench.KgToNextRank);
    }

    [Fact]
    public async Task GetStrengthStandardsOverviewAsync_WithRankFilter_FiltersExerciseStandardsCorrectly()
    {
        var result = await _service.GetStrengthStandardsOverviewAsync(AthleteId, "Любитель");

        Assert.True(result.IsSuccess);
        var standards = result.Value!.ExerciseStandards;

        var single = Assert.Single(standards);
        Assert.Equal(BenchPressId, single.ExerciseId);
        Assert.Equal("Любитель", single.StrengthRankTitle);
    }

    [Fact]
    public async Task GetStrengthStandardsOverviewAsync_CalculatesPushPullLegsSymmetryScoreAndAdvice()
    {
        var result = await _service.GetStrengthStandardsOverviewAsync(AthleteId);

        Assert.True(result.IsSuccess);
        var overview = result.Value!;

        Assert.Equal(33, overview.PushSharePercent);
        Assert.Equal(33, overview.PullSharePercent);
        Assert.Equal(34, overview.LegsSharePercent);
        Assert.True(overview.SymmetryScorePercent >= 95);
        Assert.Contains("Відмінна симетрія", overview.SymmetryAdviceText);
    }

    [Theory]
    [InlineData(0, 10, 5, "userId")]
    [InlineData(-1, 10, 5, "userId")]
    [InlineData(1, 0, 5, "exerciseId")]
    [InlineData(1, -3, 5, "exerciseId")]
    [InlineData(1, 10, 0, "targetReps")]
    [InlineData(1, 10, 25, "targetReps")]
    public async Task CalculateNextStandardTargetAsync_WhenParametersInvalid_LogsWarningAndReturnsValidationFailure(
        int userId,
        int exerciseId,
        int targetReps,
        string expectedField)
    {
        var result = await _service.CalculateNextStandardTargetAsync(userId, exerciseId, targetReps);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(expectedField, result.Field);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task CalculateNextStandardTargetAsync_WhenUserNotFound_LogsWarningAndReturnsUserNotFound()
    {
        _userRepository
            .Setup(u => u.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _service.CalculateNextStandardTargetAsync(999, BenchPressId, 5);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, result.ErrorCode);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task CalculateNextStandardTargetAsync_WhenExerciseNotFound_LogsWarningAndReturnsExerciseNotFound()
    {
        _exerciseRepository
            .Setup(e => e.GetByIdAsync(777, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Exercise?)null);

        var result = await _service.CalculateNextStandardTargetAsync(AthleteId, 777, 5);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ExerciseNotFound, result.ErrorCode);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task CalculateNextStandardTargetAsync_ValidRequest_CalculatesTarget1RmAndWorkingWeight()
    {
        var result = await _service.CalculateNextStandardTargetAsync(AthleteId, BenchPressId, 6);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var plan = result.Value!;

        Assert.Equal(BenchPressId, plan.ExerciseId);
        Assert.Equal("Любитель", plan.CurrentRankTitle);
        Assert.Equal("Атлет", plan.TargetRankTitle);
        Assert.Equal(100m, plan.TargetOneRepMaxKg);
        Assert.Equal(6, plan.TargetReps);
        Assert.Equal(83.3m, plan.RecommendedWorkingWeightKg);
        Assert.Contains("83.3 кг × 6 повт.", plan.PlanSummaryText);
    }

    private static IReadOnlyList<WorkoutRecord> CreateSampleRecords()
    {
        return new List<WorkoutRecord>
        {
            new()
            {
                Id = 1,
                ExerciseId = BenchPressId,
                IsSuspicious = false,
                CreatedAt = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Weight = 75m, Reps = 6 },
                },
            },
            new()
            {
                Id = 2,
                ExerciseId = BenchPressId,
                IsSuspicious = true,
                CreatedAt = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Weight = 500m, Reps = 10 },
                },
            },
            new()
            {
                Id = 3,
                ExerciseId = SquatId,
                IsSuspicious = false,
                CreatedAt = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Weight = 120m, Reps = 5 },
                },
            },
        };
    }
}
