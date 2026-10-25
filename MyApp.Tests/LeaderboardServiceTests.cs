using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;

namespace MyApp.Tests;

public class LeaderboardServiceTests
{
    private readonly Mock<IUserProgressRepository> _progressRepositoryMock = new();
    private readonly Mock<ILogger<LeaderboardService>> _loggerMock = new();
    private readonly LeaderboardService _service;

    public LeaderboardServiceTests()
    {
        _service = new LeaderboardService(
            _progressRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetLeaderboardAsync_SortsByXpDescendingAndAssignsSequentialRanks()
    {
        // Arrange
        var users = new List<User>
        {
            CreateUser(1, "athlete", level: 1, xp: 600),
            CreateUser(2, "iron_max", level: 3, xp: 2450),
            CreateUser(3, "power_oleh", level: 2, xp: 1500),
        };
        _progressRepositoryMock
            .Setup(r => r.GetLeaderboardUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _service.GetLeaderboardAsync(currentUserId: 1);

        // Assert
        Assert.Equal(3, result.TotalParticipants);
        Assert.Equal(3, result.Entries.Count);

        Assert.Equal(1, result.Entries[0].Rank);
        Assert.Equal("iron_max", result.Entries[0].Username);
        Assert.Equal(2450, result.Entries[0].TotalXp);

        Assert.Equal(2, result.Entries[1].Rank);
        Assert.Equal("power_oleh", result.Entries[1].Username);
        Assert.Equal(1500, result.Entries[1].TotalXp);

        Assert.Equal(3, result.Entries[2].Rank);
        Assert.Equal("athlete", result.Entries[2].Username);
        Assert.True(result.Entries[2].IsCurrentUser);
        Assert.Equal(3, result.CurrentUserRank);
        Assert.Equal(600, result.CurrentUserXp);
    }

    [Fact]
    public async Task GetLeaderboardAsync_ExcludesBlockedAndExcludedFromLeaderboardUsers()
    {
        // Arrange
        var users = new List<User>
        {
            CreateUser(1, "active_athlete", level: 2, xp: 1200),
            CreateUser(2, "blocked_user", level: 5, xp: 5000, isBlocked: true),
            CreateUser(3, "hidden_cheater", level: 10, xp: 9999, isExcluded: true),
        };
        _progressRepositoryMock
            .Setup(r => r.GetLeaderboardUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _service.GetLeaderboardAsync(currentUserId: 1);

        // Assert
        Assert.Equal(1, result.TotalParticipants);
        Assert.Single(result.Entries);
        Assert.Equal("active_athlete", result.Entries[0].Username);
        Assert.Equal(1, result.Entries[0].Rank);
        Assert.Equal(1, result.CurrentUserRank);
    }

    [Fact]
    public async Task GetLeaderboardAsync_WhenUserHasNoProgressRecord_DefaultsToLevelOneAndZeroXp()
    {
        // Arrange
        var userWithoutProgress = new User
        {
            Id = 5,
            Username = "newbie",
            UserProgress = null,
        };
        _progressRepositoryMock
            .Setup(r => r.GetLeaderboardUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { userWithoutProgress });

        // Act
        var result = await _service.GetLeaderboardAsync(currentUserId: 5);

        // Assert
        Assert.Single(result.Entries);
        Assert.Equal(1, result.Entries[0].Level);
        Assert.Equal(0, result.Entries[0].TotalXp);
        Assert.Equal("Новачок залу", result.Entries[0].RankTitle);
        Assert.Equal(1, result.CurrentUserRank);
    }

    [Fact]
    public async Task GetLeaderboardAsync_WhenSearchQueryProvided_FiltersByUsernameButPreservesGlobalRank()
    {
        // Arrange
        var users = new List<User>
        {
            CreateUser(1, "iron_max", level: 3, xp: 2450),
            CreateUser(2, "power_oleh", level: 2, xp: 1650),
            CreateUser(3, "athlete", level: 1, xp: 500),
        };
        _progressRepositoryMock
            .Setup(r => r.GetLeaderboardUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _service.GetLeaderboardAsync(currentUserId: 3, searchQuery: "  OLEH ");

        // Assert
        Assert.Equal(3, result.TotalParticipants);
        Assert.Equal(3, result.CurrentUserRank);
        Assert.Single(result.Entries);
        Assert.Equal("power_oleh", result.Entries[0].Username);
        Assert.Equal(2, result.Entries[0].Rank);
    }

    [Fact]
    public async Task GetLeaderboardAsync_WhenXpIsEqual_BreaksTiesAlphabeticallyByUsername()
    {
        // Arrange
        var users = new List<User>
        {
            CreateUser(1, "zorian", level: 2, xp: 1500),
            CreateUser(2, "andriy", level: 2, xp: 1500),
            CreateUser(3, "bogdan", level: 2, xp: 1500),
        };
        _progressRepositoryMock
            .Setup(r => r.GetLeaderboardUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _service.GetLeaderboardAsync(currentUserId: 1);

        // Assert
        Assert.Equal("andriy", result.Entries[0].Username);
        Assert.Equal(1, result.Entries[0].Rank);
        Assert.Equal("bogdan", result.Entries[1].Username);
        Assert.Equal(2, result.Entries[1].Rank);
        Assert.Equal("zorian", result.Entries[2].Username);
        Assert.Equal(3, result.Entries[2].Rank);
    }

    [Fact]
    public async Task GetLeaderboardAsync_WhenCurrentUserNotFound_ReturnsNullCurrentUserRank()
    {
        // Arrange
        var users = new List<User>
        {
            CreateUser(1, "iron_max", level: 3, xp: 2450),
        };
        _progressRepositoryMock
            .Setup(r => r.GetLeaderboardUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _service.GetLeaderboardAsync(currentUserId: 999);

        // Assert
        Assert.Null(result.CurrentUserRank);
        Assert.Equal(1, result.CurrentUserLevel);
        Assert.Equal(0, result.CurrentUserXp);
        Assert.All(result.Entries, e => Assert.False(e.IsCurrentUser));
    }

    [Theory]
    [InlineData(1, "Новачок залу")]
    [InlineData(2, "Залізний аматор")]
    [InlineData(3, "Залізний аматор")]
    [InlineData(4, "Досвідчений атлет")]
    [InlineData(5, "Досвідчений атлет")]
    [InlineData(8, "Ветеран штанги")]
    [InlineData(12, "Легенда PulseForge")]
    public void GetRankTitle_ReturnsExpectedTitleForLevel(int level, string expectedTitle)
    {
        // Act
        var title = LeaderboardService.GetRankTitle(level);

        // Assert
        Assert.Equal(expectedTitle, title);
    }

    private static User CreateUser(
        long id,
        string username,
        int level,
        long xp,
        bool isBlocked = false,
        bool isExcluded = false)
    {
        return new User
        {
            Id = id,
            Username = username,
            Email = $"{username}@gym.com",
            IsBlocked = isBlocked,
            IsExcludedFromLeaderboard = isExcluded,
            UserProgress = new UserProgress
            {
                UserId = id,
                Level = level,
                Xp = xp,
            },
        };
    }
}
