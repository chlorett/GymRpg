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

public sealed class PersonalRecordServiceTests
{
    private const int AthleteId = 1;
    private const int BenchPressId = 10;
    private const int SquatId = 20;

    private readonly Mock<IPersonalRecordRepository> _recordRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserProgressRepository> _progressRepository = new();
    private readonly Mock<IExerciseRepository> _exerciseRepository = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 14, 18, 0, 0, TimeSpan.Zero));
    private readonly FakeLogger<PersonalRecordService> _logger = new();
    private readonly PersonalRecordService _service;

    public PersonalRecordServiceTests()
    {
        _userRepository
            .Setup(u => u.GetByIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = AthleteId, Username = "athlete" });

        _exerciseRepository
            .Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Exercise>
            {
                new() { Id = BenchPressId, Name = "Жим штанги лежачи" },
                new() { Id = SquatId, Name = "Присідання зі штангою" },
            });

        _service = new PersonalRecordService(
            _recordRepository.Object,
            _userRepository.Object,
            _progressRepository.Object,
            _exerciseRepository.Object,
            _timeProvider,
            _logger);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(80, 0, 0)]
    [InlineData(100, 1, 100)]
    [InlineData(90, 10, 120)]
    [InlineData(120, 5, 140)]
    public void CalculateEpleyOneRepMax_ReturnsExpectedRoundedValue(
        decimal weightKg,
        int reps,
        decimal expectedOneRepMax)
    {
        decimal result = PersonalRecordService.CalculateEpleyOneRepMax(weightKg, reps);

        Assert.Equal(expectedOneRepMax, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task GetPersonalRecordsOverviewAsync_WhenUserIdInvalid_LogsWarningAndReturnsFailure(int invalidUserId)
    {
        var result = await _service.GetPersonalRecordsOverviewAsync(invalidUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("userId", result.Field);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetPersonalRecordsOverviewAsync_WhenExerciseFilterInvalid_LogsWarningAndReturnsFailure(int invalidExerciseId)
    {
        var result = await _service.GetPersonalRecordsOverviewAsync(AthleteId, invalidExerciseId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("exerciseIdFilter", result.Field);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task GetPersonalRecordsOverviewAsync_CalculatesPersonalRecordsAndIgnoresSuspiciousInPrTable()
    {
        _recordRepository
            .Setup(r => r.GetAllByUserIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleRecords());

        var result = await _service.GetPersonalRecordsOverviewAsync(AthleteId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var overview = result.Value!;

        Assert.Equal(3, overview.TotalWorkoutsLogged);
        Assert.Equal(2, overview.ExercisesWithRecordsCount);
        Assert.Equal(140m, overview.BestOneRepMaxKg);
        Assert.Equal("Присідання зі штангою", overview.BestExerciseName);
        Assert.Equal(2, overview.PersonalRecords.Count);
        Assert.Equal(3, overview.WorkoutLogs.Count);

        var benchPr = overview.PersonalRecords.First(p => p.ExerciseId == BenchPressId);
        Assert.Equal(90m, benchPr.MaxWeightKg);
        Assert.Equal(120m, benchPr.EstimatedOneRepMaxKg);
        Assert.Equal(1, benchPr.TotalWorkoutsCount);
    }

    [Fact]
    public async Task GetPersonalRecordsOverviewAsync_WithExerciseAndHonestyFilter_FiltersLogsCorrectly()
    {
        _recordRepository
            .Setup(r => r.GetAllByUserIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleRecords());

        var result = await _service.GetPersonalRecordsOverviewAsync(
            AthleteId,
            exerciseIdFilter: BenchPressId,
            honestyFilter: "Підозрілі");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var overview = result.Value!;

        Assert.Single(overview.WorkoutLogs);
        Assert.True(overview.WorkoutLogs[0].IsSuspicious);
        Assert.Equal("Підозрілий", overview.WorkoutLogs[0].StatusText);
        Assert.Single(overview.PersonalRecords);
    }

    [Theory]
    [InlineData(0, 5, "userId")]
    [InlineData(-1, 5, "userId")]
    [InlineData(1, 0, "recordId")]
    [InlineData(1, -2, "recordId")]
    public async Task DeleteWorkoutRecordAsync_WhenIdsInvalid_ReturnsValidationFailedAndLogsWarning(
        int userId,
        int recordId,
        string expectedField)
    {
        var result = await _service.DeleteWorkoutRecordAsync(userId, recordId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(expectedField, result.Field);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        _recordRepository.Verify(r => r.DeleteRecordAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteWorkoutRecordAsync_WhenUserNotFound_ReturnsUserNotFound()
    {
        _userRepository
            .Setup(u => u.GetByIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _service.DeleteWorkoutRecordAsync(AthleteId, 100);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, result.ErrorCode);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task DeleteWorkoutRecordAsync_WhenRecordNotFound_ReturnsRecordNotFound()
    {
        _recordRepository
            .Setup(r => r.GetByIdWithDetailsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkoutRecord?)null);

        var result = await _service.DeleteWorkoutRecordAsync(AthleteId, 999);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RecordNotFound, result.ErrorCode);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task DeleteWorkoutRecordAsync_WhenRecordBelongsToAnotherUser_ReturnsRecordAccessDenied()
    {
        var foreignRecord = new WorkoutRecord
        {
            Id = 55,
            ExerciseId = BenchPressId,
            XpEarned = 120,
            Session = new WorkoutSession
            {
                Workout = new Workout { UserId = 999 },
            },
        };

        _recordRepository
            .Setup(r => r.GetByIdWithDetailsAsync(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignRecord);

        var result = await _service.DeleteWorkoutRecordAsync(AthleteId, 55);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RecordAccessDenied, result.ErrorCode);
        Assert.Null(result.Value);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        _recordRepository.Verify(r => r.DeleteRecordAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteWorkoutRecordAsync_WhenFairRecordDeleted_DeletesAndRecalculatesUserProgress()
    {
        var ownRecord = new WorkoutRecord
        {
            Id = 101,
            ExerciseId = BenchPressId,
            XpEarned = 300,
            IsSuspicious = false,
            Session = new WorkoutSession
            {
                Workout = new Workout { UserId = AthleteId },
            },
        };

        var userProgress = new UserProgress
        {
            UserId = AthleteId,
            Xp = 1200,
            Level = 2,
        };

        _recordRepository
            .Setup(r => r.GetByIdWithDetailsAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownRecord);
        _recordRepository
            .Setup(r => r.DeleteRecordAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _progressRepository
            .Setup(p => p.GetByUserIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userProgress);

        var result = await _service.DeleteWorkoutRecordAsync(AthleteId, 101);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(101, result.Value!.DeletedRecordId);
        Assert.Equal(900, result.Value.AdjustedXp);
        Assert.Equal(1, result.Value.NewLevel);
        _progressRepository.Verify(
            p => p.SaveAsync(
                It.Is<UserProgress>(up => up.Xp == 900 && up.Level == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static IReadOnlyList<WorkoutRecord> CreateSampleRecords()
    {
        return new List<WorkoutRecord>
        {
            new()
            {
                Id = 1,
                ExerciseId = BenchPressId,
                XpEarned = 180,
                IsSuspicious = false,
                CreatedAt = new DateTimeOffset(2026, 11, 10, 14, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Weight = 80m, Reps = 10 },
                    new() { SetNumber = 2, Weight = 90m, Reps = 10 },
                },
            },
            new()
            {
                Id = 2,
                ExerciseId = BenchPressId,
                XpEarned = 0,
                IsSuspicious = true,
                CreatedAt = new DateTimeOffset(2026, 11, 11, 15, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Weight = 500m, Reps = 20 },
                },
            },
            new()
            {
                Id = 3,
                ExerciseId = SquatId,
                XpEarned = 220,
                IsSuspicious = false,
                CreatedAt = new DateTimeOffset(2026, 11, 12, 16, 0, 0, TimeSpan.Zero),
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Weight = 120m, Reps = 5 },
                },
            },
        };
    }
}
