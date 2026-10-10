using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;

namespace MyApp.Tests;

public class WorkoutTrackingServiceTests
{
    private readonly Mock<IWorkoutRecordRepository> _recordRepositoryMock = new();
    private readonly Mock<IUserProgressRepository> _progressRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IExerciseRepository> _exerciseRepositoryMock = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly Mock<ILogger<WorkoutTrackingService>> _loggerMock = new();
    private readonly WorkoutTrackingService _service;

    public WorkoutTrackingServiceTests()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

        _service = new WorkoutTrackingService(
            _recordRepositoryMock.Object,
            _progressRepositoryMock.Object,
            _timeProvider,
            _loggerMock.Object,
            _userRepositoryMock.Object,
            _exerciseRepositoryMock.Object);
    }

    [Fact]
    public async Task TrackAsync_WhenRequestIsNull_ReturnsValidationFailed()
    {
        // Act
        var result = await _service.TrackAsync(null!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TrackAsync_WhenUserIdInvalid_ReturnsValidationFailed(int userId)
    {
        // Arrange
        var request = new TrackWorkoutRequest(userId, 1, new[] { new WorkoutSetDto(10, 50m) });

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(TrackWorkoutRequest.UserId), result.Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TrackAsync_WhenExerciseIdInvalid_ReturnsValidationFailed(int exerciseId)
    {
        // Arrange
        var request = new TrackWorkoutRequest(1, exerciseId, new[] { new WorkoutSetDto(10, 50m) });

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(TrackWorkoutRequest.ExerciseId), result.Field);
    }

    [Fact]
    public async Task TrackAsync_WhenSetsEmptyOrNull_ReturnsValidationFailed()
    {
        // Arrange
        var request = new TrackWorkoutRequest(1, 1, Array.Empty<WorkoutSetDto>());

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(TrackWorkoutRequest.Sets), result.Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task TrackAsync_WhenSetRepsNonPositive_ReturnsValidationFailed(int reps)
    {
        // Arrange
        var request = new TrackWorkoutRequest(1, 1, new[] { new WorkoutSetDto(reps, 50m) });

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("Reps", result.Field);
    }

    [Fact]
    public async Task TrackAsync_WhenSetRepsExceedsUpperLimit_ReturnsValidationFailed()
    {
        // Arrange
        var request = new TrackWorkoutRequest(1, 1, new[] { new WorkoutSetDto(600, 20m) });

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("Reps", result.Field);
    }

    [Fact]
    public async Task TrackAsync_WhenSetWeightNegative_ReturnsValidationFailed()
    {
        // Arrange
        var request = new TrackWorkoutRequest(1, 1, new[] { new WorkoutSetDto(10, -10m) });

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("Weight", result.Field);
    }

    [Fact]
    public async Task TrackAsync_WhenSetWeightExceedsUpperLimit_ReturnsValidationFailed()
    {
        // Arrange
        var request = new TrackWorkoutRequest(1, 1, new[] { new WorkoutSetDto(10, 1500m) });

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("Weight", result.Field);
    }

    [Fact]
    public async Task TrackAsync_WhenUserNotFound_ReturnsUserNotFoundErrorCode()
    {
        // Arrange
        var request = new TrackWorkoutRequest(999, 1, new[] { new WorkoutSetDto(10, 50m) });
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, result.ErrorCode);
        Assert.Equal(nameof(TrackWorkoutRequest.UserId), result.Field);
    }

    [Fact]
    public async Task TrackAsync_WhenExerciseNotFound_ReturnsExerciseNotFoundErrorCode()
    {
        // Arrange
        var request = new TrackWorkoutRequest(1, 404, new[] { new WorkoutSetDto(10, 50m) });
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "TestUser" });
        _exerciseRepositoryMock
            .Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Exercise?)null);

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ExerciseNotFound, result.ErrorCode);
        Assert.Equal(nameof(TrackWorkoutRequest.ExerciseId), result.Field);
    }

    [Fact]
    public async Task TrackAsync_WhenFirstWorkout_CreatesNewProgressAndSavesRecord()
    {
        // Arrange
        var request = new TrackWorkoutRequest(1, 10, new[]
        {
            new WorkoutSetDto(10, 50m), // 500 XP
        });

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "Hero" });
        _exerciseRepositoryMock
            .Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Exercise { Id = 10, Name = "Жим лежачи" });
        _progressRepositoryMock
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProgress?)null);

        UserProgress? savedProgress = null;
        _progressRepositoryMock
            .Setup(r => r.SaveAsync(It.IsAny<UserProgress>(), It.IsAny<CancellationToken>()))
            .Callback<UserProgress, CancellationToken>((p, _) => savedProgress = p)
            .Returns(Task.CompletedTask);

        WorkoutRecord? savedRecord = null;
        _recordRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<WorkoutRecord>(), It.IsAny<CancellationToken>()))
            .Callback<WorkoutRecord, CancellationToken>((r, _) => savedRecord = r)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(savedProgress);
        Assert.Equal(1, savedProgress.UserId);
        Assert.Equal(500, savedProgress.Xp);
        Assert.Equal(1, savedProgress.Level);

        Assert.NotNull(savedRecord);
        Assert.Equal(1, savedRecord.UserId);
        Assert.Equal(10, savedRecord.ExerciseId);
        Assert.Equal(500, savedRecord.XpEarned);
        Assert.False(savedRecord.IsSuspicious);
        Assert.Single(savedRecord.Sets);
        Assert.Equal(10, savedRecord.Sets[0].Reps);
        Assert.Equal(50m, savedRecord.Sets[0].Weight);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, savedRecord.CreatedAtUtc);
    }

    [Fact]
    public async Task TrackAsync_WhenUserHasExistingProgress_IncrementsXpAndUpdatesLevel()
    {
        // Arrange
        var existingProgress = new UserProgress { Id = 5, UserId = 1, Xp = 800, Level = 1 };
        var request = new TrackWorkoutRequest(1, 10, new[]
        {
            new WorkoutSetDto(10, 50m), // 500 XP -> Total: 1300 XP -> Level 2
        });

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "Hero" });
        _exerciseRepositoryMock
            .Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Exercise { Id = 10, Name = "Присідання" });
        _progressRepositoryMock
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProgress);

        _progressRepositoryMock
            .Setup(r => r.SaveAsync(It.IsAny<UserProgress>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _recordRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<WorkoutRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1300, existingProgress.Xp);
        Assert.Equal(2, existingProgress.Level);
        _progressRepositoryMock.Verify(r => r.SaveAsync(existingProgress, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrackAsync_WhenAbnormalWeightEntered_MarksRecordAsSuspicious()
    {
        // Arrange
        var request = new TrackWorkoutRequest(1, 10, new[]
        {
            new WorkoutSetDto(5, 450m), // 450kg > 400kg threshold
        });

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "Strongman" });
        _exerciseRepositoryMock
            .Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Exercise { Id = 10, Name = "Станова тяга" });
        _progressRepositoryMock
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProgress { UserId = 1, Xp = 0, Level = 1 });

        WorkoutRecord? savedRecord = null;
        _recordRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<WorkoutRecord>(), It.IsAny<CancellationToken>()))
            .Callback<WorkoutRecord, CancellationToken>((r, _) => savedRecord = r)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.TrackAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(savedRecord);
        Assert.True(savedRecord.IsSuspicious);
    }
}
