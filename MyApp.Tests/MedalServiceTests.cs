using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Tests;

public class MedalServiceTests
{
    private readonly Mock<IMedalRepository> _medalRepositoryMock = new();
    private readonly Mock<IUserProgressRepository> _progressRepositoryMock = new();
    private readonly Mock<IWorkoutRecordRepository> _workoutRecordRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ILogger<MedalService>> _loggerMock = new();
    private readonly MedalService _service;

    public MedalServiceTests()
    {
        _service = new MedalService(
            _medalRepositoryMock.Object,
            _progressRepositoryMock.Object,
            _workoutRecordRepositoryMock.Object,
            _userRepositoryMock.Object,
            TimeProvider.System,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetUserMedalsOverviewAsync_CalculatesProgressAndEarnedStatsCorrectly() /*перевіряє правильність підрахунку здобутих медалей, прогресу та доступних до отримання нагород*/
    {
        var medals = new List<Medal>
        {
            CreateMedal(1, "Перший крок у залі", MedalConditionType.WorkoutsCount, 1),
            CreateMedal(2, "Перша тисяча XP", MedalConditionType.TotalXp, 1000),
            CreateMedal(3, "Досвідчений атлет", MedalConditionType.Level, 5),
        };
        var userMedals = new List<UserMedal>
        {
            new() { Id = 1, UserId = 1, MedalId = 1, EarnedAt = DateTimeOffset.UtcNow.AddDays(-2) },
        };

        SetupOverviewMocks(
            userId: 1,
            medals: medals,
            userMedals: userMedals,
            progress: new UserProgress { UserId = 1, Level = 2, Xp = 1200 },
            records: new List<WorkoutRecord>
            {
                new() { Id = 10, ExerciseId = 1, CreatedAt = DateTimeOffset.UtcNow, XpEarned = 1200 },
            });

        var result = await _service.GetUserMedalsOverviewAsync(userId: 1);

        Assert.Equal(3, result.TotalMedalsCount);
        Assert.Equal(1, result.EarnedMedalsCount);
        Assert.Equal(1, result.AvailableToClaimCount);
        Assert.Equal(33, result.CompletionPercent);
        Assert.Equal(3, result.Medals.Count);

        var firstStepMedal = result.Medals.First(m => m.MedalId == 1);
        Assert.True(firstStepMedal.IsEarned);
        Assert.False(firstStepMedal.CanClaim);
        Assert.Equal(100, firstStepMedal.ProgressPercent);

        var xpMedal = result.Medals.First(m => m.MedalId == 2);
        Assert.False(xpMedal.IsEarned);
        Assert.True(xpMedal.CanClaim);
        Assert.Equal(100, xpMedal.ProgressPercent);
        Assert.Equal("Готово до отримання!", xpMedal.StatusText);

        var levelMedal = result.Medals.First(m => m.MedalId == 3);
        Assert.False(levelMedal.IsEarned);
        Assert.False(levelMedal.CanClaim);
        Assert.Equal(40, levelMedal.ProgressPercent);
        Assert.Equal("Заблоковано", levelMedal.StatusText);
    }

    [Fact]
    public async Task GetUserMedalsOverviewAsync_WhenStatusFilterProvided_FiltersEarnedAndLockedMedals() /*перевіряє фільтрацію медалей за статусом «Здобуті» та «Заблоковані» зі збереженням загальної статистики*/
    {
        var medals = new List<Medal>
        {
            CreateMedal(1, "Перший крок", MedalConditionType.WorkoutsCount, 1),
            CreateMedal(2, "Перша тисяча XP", MedalConditionType.TotalXp, 1000),
        };
        var userMedals = new List<UserMedal>
        {
            new() { Id = 1, UserId = 1, MedalId = 1, EarnedAt = DateTimeOffset.UtcNow },
        };

        SetupOverviewMocks(
            userId: 1,
            medals: medals,
            userMedals: userMedals,
            progress: new UserProgress { UserId = 1, Level = 1, Xp = 400 },
            records: new List<WorkoutRecord>());

        var earnedOnly = await _service.GetUserMedalsOverviewAsync(userId: 1, statusFilter: "Здобуті");
        var lockedOnly = await _service.GetUserMedalsOverviewAsync(userId: 1, statusFilter: "Заблоковані");

        Assert.Equal(2, earnedOnly.TotalMedalsCount);
        Assert.Equal(1, earnedOnly.EarnedMedalsCount);
        Assert.Single(earnedOnly.Medals);
        Assert.Equal("Перший крок", earnedOnly.Medals[0].Name);

        Assert.Single(lockedOnly.Medals);
        Assert.Equal("Перша тисяча XP", lockedOnly.Medals[0].Name);
    }

    [Fact]
    public async Task GetUserMedalsOverviewAsync_IgnoresSuspiciousWorkoutsForWorkoutAndStreakCounts() /*перевіряє що підозрілі тренування (античіт IsSuspicious) не зараховуються для отримання медалей*/
    {
        var medals = new List<Medal>
        {
            CreateMedal(1, "Перший крок", MedalConditionType.WorkoutsCount, 1),
        };

        SetupOverviewMocks(
            userId: 1,
            medals: medals,
            userMedals: new List<UserMedal>(),
            progress: new UserProgress { UserId = 1, Level = 1, Xp = 100 },
            records: new List<WorkoutRecord>
            {
                new() { Id = 1, ExerciseId = 1, IsSuspicious = true, CreatedAt = DateTimeOffset.UtcNow },
            });

        var result = await _service.GetUserMedalsOverviewAsync(userId: 1);

        Assert.Single(result.Medals);
        Assert.Equal(0, result.Medals[0].CurrentValue);
        Assert.False(result.Medals[0].CanClaim);
    }

    [Fact]
    public async Task ClaimEligibleMedalsAsync_WhenConditionsMet_ClaimsOnlyNewEligibleMedals() /*перевіряє автоматичне присвоєння виконаних медалей та захист від повторного видавання вже здобутих*/
    {
        var medals = new List<Medal>
        {
            CreateMedal(1, "Перший крок у залі", MedalConditionType.WorkoutsCount, 1),
            CreateMedal(2, "Перша тисяча XP", MedalConditionType.TotalXp, 1000),
            CreateMedal(3, "Титан досвіду", MedalConditionType.TotalXp, 5000),
        };
        var existingUserMedals = new List<UserMedal>
        {
            new() { Id = 1, UserId = 1, MedalId = 1, EarnedAt = DateTimeOffset.UtcNow.AddDays(-5) },
        };

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "athlete" });

        SetupOverviewMocks(
            userId: 1,
            medals: medals,
            userMedals: existingUserMedals,
            progress: new UserProgress { UserId = 1, Level = 2, Xp = 1500 },
            records: new List<WorkoutRecord>
            {
                new() { Id = 1, ExerciseId = 1, IsSuspicious = false, CreatedAt = DateTimeOffset.UtcNow },
            });

        IReadOnlyList<UserMedal>? savedMedals = null;
        _medalRepositoryMock
            .Setup(r => r.AddUserMedalsAsync(It.IsAny<IReadOnlyList<UserMedal>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<UserMedal>, CancellationToken>((items, _) => savedMedals = items)
            .Returns(Task.CompletedTask);

        var claimResult = await _service.ClaimEligibleMedalsAsync(userId: 1);

        Assert.True(claimResult.Result.IsSuccess);
        Assert.Equal(1, claimResult.NewlyClaimedCount);
        Assert.Single(claimResult.NewlyClaimedNames);
        Assert.Equal("Перша тисяча XP", claimResult.NewlyClaimedNames[0]);
        Assert.NotNull(savedMedals);
        Assert.Single(savedMedals);
        Assert.Equal(2, savedMedals[0].MedalId);
    }

    [Fact]
    public async Task ClaimEligibleMedalsAsync_WhenInvalidUserId_ReturnsValidationFailureResult() /*перевіряє повернення OperationResult.Fail при некоректному ідентифікаторі користувача*/
    {
        var claimResult = await _service.ClaimEligibleMedalsAsync(userId: 0);

        Assert.False(claimResult.Result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, claimResult.Result.ErrorCode);
        Assert.Equal(0, claimResult.NewlyClaimedCount);
    }

    [Fact]
    public async Task ClaimEligibleMedalsAsync_WhenUserDoesNotExist_ReturnsUserNotFoundFailureResult() /*перевіряє повернення OperationResult.Fail з кодом UserNotFound, якщо користувача немає в системі*/
    {
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var claimResult = await _service.ClaimEligibleMedalsAsync(userId: 999);

        Assert.False(claimResult.Result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, claimResult.Result.ErrorCode);
        Assert.Equal(0, claimResult.NewlyClaimedCount);
    }

    [Theory]
    [InlineData(MedalConditionType.WorkoutsCount, "Тренування")]
    [InlineData(MedalConditionType.Level, "Рівень")]
    [InlineData(MedalConditionType.TotalXp, "Досвід (XP)")]
    [InlineData(MedalConditionType.StreakDays, "Дні активності")]
    public void GetConditionCategoryText_ReturnsExpectedCategoryLabel(
        MedalConditionType conditionType,
        string expectedCategory) /*перевіряє коректність текстової назви категорії умови медалі*/
    {
        string category = MedalService.GetConditionCategoryText(conditionType);

        Assert.Equal(expectedCategory, category);
    }

    private static Medal CreateMedal(
        int id,
        string name,
        MedalConditionType conditionType,
        int conditionValue)
    {
        return new Medal
        {
            Id = id,
            Name = name,
            Description = $"Умова: {name}",
            ConditionType = conditionType,
            ConditionValue = conditionValue,
        };
    }

    private void SetupOverviewMocks(
        int userId,
        IReadOnlyList<Medal> medals,
        IReadOnlyList<UserMedal> userMedals,
        UserProgress? progress,
        IReadOnlyList<WorkoutRecord> records)
    {
        _medalRepositoryMock
            .Setup(r => r.GetAllMedalsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(medals);
        _medalRepositoryMock
            .Setup(r => r.GetUserMedalsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userMedals);
        _progressRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);
        _workoutRecordRepositoryMock
            .Setup(r => r.GetRecentByUserIdAsync(userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);
    }
}
