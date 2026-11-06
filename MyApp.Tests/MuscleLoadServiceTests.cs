using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;

namespace MyApp.Tests;

public class MuscleLoadServiceTests
{
    private readonly Mock<IMuscleLoadRepository> _muscleLoadRepositoryMock = new();
    private readonly Mock<IWorkoutRecordRepository> _workoutRecordRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ILogger<MuscleLoadService>> _loggerMock = new();
    private readonly MuscleLoadService _service;

    public MuscleLoadServiceTests()
    {
        _service = new MuscleLoadService(
            _muscleLoadRepositoryMock.Object,
            _workoutRecordRepositoryMock.Object,
            _userRepositoryMock.Object,
            TimeProvider.System,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetUserMuscleLoadOverviewAsync_CalculatesSharesAndTopMuscleGroupCorrectly()
    {
        var groups = new List<MuscleGroup>
        {
            new() { Id = 1, Name = "Грудні м'язи" },
            new() { Id = 2, Name = "М'язи спини" },
            new() { Id = 3, Name = "М'язи кора" },
        };
        var links = new List<ExerciseMuscleGroup>
        {
            new() { ExerciseId = 1, MuscleGroupId = 1, LoadFactor = 1.0m },
            new() { ExerciseId = 2, MuscleGroupId = 1, LoadFactor = 0.8m },
            new() { ExerciseId = 3, MuscleGroupId = 2, LoadFactor = 1.0m },
        };
        var storedLoads = new List<MuscleLoad>
        {
            new() { Id = 1, UserId = 1, MuscleGroupId = 1, LoadValue = 300m, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = 2, UserId = 1, MuscleGroupId = 2, LoadValue = 100m, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = 3, UserId = 1, MuscleGroupId = 3, LoadValue = 0m, UpdatedAt = DateTimeOffset.UtcNow },
        };

        SetupOverviewMocks(1, groups, links, storedLoads);

        var overview = await _service.GetUserMuscleLoadOverviewAsync(userId: 1);

        Assert.Equal(3, overview.TotalGroupsCount);
        Assert.Equal(2, overview.ActiveGroupsCount);
        Assert.Equal(400m, overview.TotalLoadValue);
        Assert.Equal("Грудні м'язи", overview.TopMuscleGroupName);
        Assert.Equal("М'язи кора", overview.LeastLoadedMuscleGroupName);
        Assert.Equal(3, overview.MuscleGroups.Count);

        var chest = overview.MuscleGroups.First(g => g.MuscleGroupId == 1);
        Assert.Equal(75, chest.SharePercent);
        Assert.Equal(2, chest.ExercisesCount);
        Assert.Equal("Високе навантаження", chest.StatusText);

        var back = overview.MuscleGroups.First(g => g.MuscleGroupId == 2);
        Assert.Equal(25, back.SharePercent);
        Assert.Equal(1, back.ExercisesCount);

        var core = overview.MuscleGroups.First(g => g.MuscleGroupId == 3);
        Assert.Equal(0, core.SharePercent);
        Assert.Equal("Без навантаження", core.StatusText);
    }

    [Fact]
    public async Task GetUserMuscleLoadOverviewAsync_WhenStatusFilterProvided_FiltersActiveAndLowGroups()
    {
        var groups = new List<MuscleGroup>
        {
            new() { Id = 1, Name = "Грудні м'язи" },
            new() { Id = 2, Name = "М'язи кора" },
        };
        var storedLoads = new List<MuscleLoad>
        {
            new() { Id = 1, UserId = 1, MuscleGroupId = 1, LoadValue = 250m, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = 2, UserId = 1, MuscleGroupId = 2, LoadValue = 0m, UpdatedAt = DateTimeOffset.UtcNow },
        };

        SetupOverviewMocks(1, groups, new List<ExerciseMuscleGroup>(), storedLoads);

        var activeOnly = await _service.GetUserMuscleLoadOverviewAsync(userId: 1, statusFilter: "Активні");
        var attentionOnly = await _service.GetUserMuscleLoadOverviewAsync(userId: 1, statusFilter: "Потребують уваги");

        Assert.Single(activeOnly.MuscleGroups);
        Assert.Equal("Грудні м'язи", activeOnly.MuscleGroups[0].MuscleGroupName);

        Assert.Single(attentionOnly.MuscleGroups);
        Assert.Equal("М'язи кора", attentionOnly.MuscleGroups[0].MuscleGroupName);
    }

    [Fact]
    public async Task GetUserMuscleLoadOverviewAsync_WhenNoStoredLoads_ComputesFromWorkoutRecords()
    {
        var groups = new List<MuscleGroup>
        {
            new() { Id = 1, Name = "Грудні м'язи" },
            new() { Id = 5, Name = "М'язи рук" },
        };
        var links = new List<ExerciseMuscleGroup>
        {
            new() { ExerciseId = 1, MuscleGroupId = 1, LoadFactor = 1.0m },
            new() { ExerciseId = 1, MuscleGroupId = 5, LoadFactor = 0.5m },
        };
        var records = new List<WorkoutRecord>
        {
            new()
            {
                Id = 10,
                ExerciseId = 1,
                IsSuspicious = false,
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Reps = 10, Weight = 80m },
                },
            },
        };

        SetupOverviewMocks(1, groups, links, new List<MuscleLoad>(), records);

        var overview = await _service.GetUserMuscleLoadOverviewAsync(userId: 1);

        Assert.Equal(2, overview.ActiveGroupsCount);
        Assert.Equal(120m, overview.TotalLoadValue);
        Assert.Equal("Грудні м'язи", overview.TopMuscleGroupName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task GetUserMuscleLoadOverviewAsync_WhenUserIdInvalid_ReturnsEmptyOverview(int invalidUserId)
    {
        var overview = await _service.GetUserMuscleLoadOverviewAsync(invalidUserId);

        Assert.Equal(0, overview.TotalGroupsCount);
        Assert.Equal(0, overview.ActiveGroupsCount);
        Assert.Equal(0m, overview.TotalLoadValue);
        Assert.Empty(overview.MuscleGroups);
        _muscleLoadRepositoryMock.Verify(
            r => r.GetAllMuscleGroupsAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RecalculateMuscleLoadAsync_WhenUserIdInvalid_ReturnsValidationError(int invalidUserId)
    {
        var result = await _service.RecalculateMuscleLoadAsync(invalidUserId);

        Assert.False(result.Result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.Result.ErrorCode);
        Assert.Equal("userId", result.Result.Field);
        _muscleLoadRepositoryMock.Verify(
            r => r.SaveUserMuscleLoadsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<MuscleLoad>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RecalculateMuscleLoadAsync_WhenUserNotFound_ReturnsUserNotFoundError()
    {
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _service.RecalculateMuscleLoadAsync(99);

        Assert.False(result.Result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, result.Result.ErrorCode);
        _muscleLoadRepositoryMock.Verify(
            r => r.SaveUserMuscleLoadsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<MuscleLoad>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RecalculateMuscleLoadAsync_WhenValidWorkoutsExist_SavesWeightedMuscleLoads()
    {
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "athlete" });

        var groups = new List<MuscleGroup>
        {
            new() { Id = 1, Name = "Грудні м'язи" },
            new() { Id = 5, Name = "М'язи рук" },
        };
        var links = new List<ExerciseMuscleGroup>
        {
            new() { ExerciseId = 1, MuscleGroupId = 1, LoadFactor = 1.0m },
            new() { ExerciseId = 1, MuscleGroupId = 5, LoadFactor = 0.5m },
        };
        var records = new List<WorkoutRecord>
        {
            new()
            {
                Id = 1,
                ExerciseId = 1,
                IsSuspicious = false,
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Reps = 10, Weight = 100m },
                    new() { SetNumber = 2, Reps = 10, Weight = 100m },
                },
            },
        };

        SetupOverviewMocks(1, groups, links, new List<MuscleLoad>(), records);

        IReadOnlyList<MuscleLoad>? savedLoads = null;
        _muscleLoadRepositoryMock
            .Setup(r => r.SaveUserMuscleLoadsAsync(1, It.IsAny<IReadOnlyList<MuscleLoad>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<MuscleLoad>, CancellationToken>((_, loads, _) => savedLoads = loads)
            .Returns(Task.CompletedTask);

        var result = await _service.RecalculateMuscleLoadAsync(1);

        Assert.True(result.Result.IsSuccess);
        Assert.Equal(2, result.UpdatedGroupsCount);
        Assert.Equal(300m, result.TotalLoadValue);
        Assert.Equal("Грудні м'язи", result.TopMuscleGroupName);
        Assert.NotNull(savedLoads);
        Assert.Equal(200m, savedLoads!.First(l => l.MuscleGroupId == 1).LoadValue);
        Assert.Equal(100m, savedLoads!.First(l => l.MuscleGroupId == 5).LoadValue);
    }

    [Fact]
    public async Task RecalculateMuscleLoadAsync_IgnoresSuspiciousWorkouts()
    {
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "athlete" });

        var groups = new List<MuscleGroup>
        {
            new() { Id = 1, Name = "Грудні м'язи" },
        };
        var links = new List<ExerciseMuscleGroup>
        {
            new() { ExerciseId = 1, MuscleGroupId = 1, LoadFactor = 1.0m },
        };
        var records = new List<WorkoutRecord>
        {
            new()
            {
                Id = 1,
                ExerciseId = 1,
                IsSuspicious = true,
                XpEarned = 5000,
                WorkoutSets = new List<WorkoutSet>
                {
                    new() { SetNumber = 1, Reps = 50, Weight = 300m },
                },
            },
        };

        SetupOverviewMocks(1, groups, links, new List<MuscleLoad>(), records);

        var result = await _service.RecalculateMuscleLoadAsync(1);

        Assert.True(result.Result.IsSuccess);
        Assert.Equal(0m, result.TotalLoadValue);
        Assert.Equal("—", result.TopMuscleGroupName);
    }

    private void SetupOverviewMocks(
        int userId,
        IReadOnlyList<MuscleGroup> groups,
        IReadOnlyList<ExerciseMuscleGroup> links,
        IReadOnlyList<MuscleLoad> storedLoads,
        IReadOnlyList<WorkoutRecord>? records = null)
    {
        _muscleLoadRepositoryMock
            .Setup(r => r.GetAllMuscleGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(groups);

        _muscleLoadRepositoryMock
            .Setup(r => r.GetAllExerciseMuscleGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(links);

        _muscleLoadRepositoryMock
            .Setup(r => r.GetUserMuscleLoadsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedLoads);

        _workoutRecordRepositoryMock
            .Setup(r => r.GetRecentByUserIdAsync(userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records ?? new List<WorkoutRecord>());
    }
}
