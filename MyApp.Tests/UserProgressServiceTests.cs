using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;

namespace MyApp.Tests;

public class UserProgressServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUserProgressRepository> _progressRepositoryMock = new();
    private readonly Mock<IWorkoutRecordRepository> _recordRepositoryMock = new();
    private readonly Mock<IExerciseRepository> _exerciseRepositoryMock = new();
    private readonly Mock<ILogger<UserProgressService>> _loggerMock = new();
    private readonly UserProgressService _service;

    public UserProgressServiceTests()
    {
        _service = new UserProgressService(
            _userRepositoryMock.Object,
            _progressRepositoryMock.Object,
            _recordRepositoryMock.Object,
            _exerciseRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetProgressAsync_WhenUserIdIsInvalid_ReturnsNull(int userId)
    {
        // Act
        var result = await _service.GetProgressAsync(userId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProgressAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.GetProgressAsync(10);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProgressAsync_WhenUserHasNoProgressYet_ReturnsDefaultLevelOneAndZeroXp()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "nazar_athlete" });

        _progressRepositoryMock
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProgress?)null);

        _recordRepositoryMock
            .Setup(r => r.GetRecentByUserIdAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WorkoutRecord>());

        _exerciseRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Exercise>());

        // Act
        var result = await _service.GetProgressAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.UserId);
        Assert.Equal("nazar_athlete", result.Username);
        Assert.Equal(1, result.Level);
        Assert.Equal(0, result.TotalXp);
        Assert.Equal(0, result.XpForCurrentLevel);
        Assert.Equal(1000, result.XpForNextLevel);
        Assert.Equal(0, result.WorkoutsCount);
        Assert.Empty(result.RecentWorkouts);
    }

    [Fact]
    public async Task GetProgressAsync_WithProgressAndWorkouts_CalculatesLevelXpAndHistoryCorrectly()
    {
        // Arrange
        var recordedTime = new DateTimeOffset(2026, 10, 17, 10, 30, 0, TimeSpan.Zero);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "nazar_pro" });

        _progressRepositoryMock
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProgress { UserId = 1, Level = 2, Xp = 1450 });

        var records = new List<WorkoutRecord>
        {
            new()
            {
                Id = 5,
                ExerciseId = 1,
                XpEarned = 800,
                CreatedAt = recordedTime,
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Reps = 10, Weight = 80m },
                },
            },
        };

        _recordRepositoryMock
            .Setup(r => r.GetRecentByUserIdAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        _exerciseRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Exercise>
            {
                new() { Id = 1, Name = "Жим лежачи на лаві" },
            });

        // Act
        var result = await _service.GetProgressAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Level);
        Assert.Equal(1450, result.TotalXp);
        Assert.Equal(450, result.XpForCurrentLevel);
        Assert.Equal(1000, result.XpForNextLevel);
        Assert.Equal(1, result.WorkoutsCount);

        var workoutItem = Assert.Single(result.RecentWorkouts);
        Assert.Equal(5, workoutItem.RecordId);
        Assert.Equal("Жим лежачи на лаві", workoutItem.ExerciseName);
        Assert.Equal(1, workoutItem.SetsCount);
        Assert.Equal(10, workoutItem.TotalReps);
        Assert.Equal(800m, workoutItem.TotalWeightKg);
        Assert.Equal(800, workoutItem.XpEarned);
        Assert.Equal(recordedTime, workoutItem.RecordedAt);
    }
}
