using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using Xunit;

namespace MyApp.Tests.Services;

public class GymModerationServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<ILogger<GymModerationService>> _loggerMock;
    private readonly GymModerationService _sut;

    public GymModerationServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<GymModerationService>>();
        _sut = new GymModerationService(_userRepoMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetGymRosterAsync_WithFiltersAndSearch_ReturnsOverview()
    {
        // Arrange
        var sampleRoster = new List<GymRosterMemberDto>
            {
                new() { UserId = 1, Nickname = "IronDenys", Role = "User", GymId = 10, DivisionId = 2, Level = 12, Xp = 3400, SuspiciousWorkoutsCount = 0, IsExcludedFromLeaderboard = false, IsBlocked = false },
                new() { UserId = 2, Nickname = "IronCheater", Role = "User", GymId = 10, DivisionId = 2, Level = 15, Xp = 5000, SuspiciousWorkoutsCount = 4, IsExcludedFromLeaderboard = true, IsBlocked = false },
                new() { UserId = 3, Nickname = "CasualLifter", Role = "User", GymId = 10, DivisionId = 2, Level = 5, Xp = 800, SuspiciousWorkoutsCount = 0, IsExcludedFromLeaderboard = false, IsBlocked = false },
                new() { UserId = 4, Nickname = "IronCoach", Role = "Trainer", GymId = 10, DivisionId = 2, Level = 20, Xp = 9000 }
            };

        _userRepoMock
            .Setup(r => r.GetGymRosterAsync(10, 2))
            .ReturnsAsync(sampleRoster);

        // Act
        var result = await _sut.GetGymRosterAsync(gymId: 10, divisionId: 2, searchQuery: "Iron");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalAthletes);
        Assert.Equal(2, result.ActiveInLeaderboard);
        Assert.Equal(1, result.ExcludedFromLeaderboard);
        Assert.Equal(3, result.Members.Count);
        Assert.Equal("IronCheater", result.Members[1].Nickname);
    }

    [Fact]
    public async Task UpdateModerationStatusAsync_WhenModeratorIsRegularUser_ReturnsFalse()
    {
        // Arrange
        var request = new UpdateAthleteModerationRequest
        {
            ModeratorId = 5,
            TargetUserId = 10,
            IsExcludedFromLeaderboard = true,
            IsBlocked = false
        };

        _userRepoMock
            .Setup(r => r.GetByIdAsync(5, default))
            .ReturnsAsync(new User { Id = 5, Username = "RegularAthlete", Role = UserRole.User });

        // Act
        var result = await _sut.UpdateModerationStatusAsync(request);

        // Assert
        Assert.False(result);
        _userRepoMock.Verify(r => r.UpdateModerationStatusAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateModerationStatusAsync_WhenModeratingSelf_ReturnsFalse()
    {
        // Arrange
        var request = new UpdateAthleteModerationRequest
        {
            ModeratorId = 7,
            TargetUserId = 7,
            IsExcludedFromLeaderboard = true,
            IsBlocked = true
        };

        // Act
        var result = await _sut.UpdateModerationStatusAsync(request);

        // Assert
        Assert.False(result);
        _userRepoMock.Verify(r => r.UpdateModerationStatusAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateModerationStatusAsync_WhenTargetIsTrainer_ReturnsFalse()
    {
        // Arrange
        var request = new UpdateAthleteModerationRequest
        {
            ModeratorId = 1,
            TargetUserId = 2,
            IsExcludedFromLeaderboard = true,
            IsBlocked = false
        };

        _userRepoMock
            .Setup(r => r.GetByIdAsync(1, default))
            .ReturnsAsync(new User { Id = 1, Username = "MainCoach", Role = UserRole.Trainer });

        _userRepoMock
            .Setup(r => r.GetByIdAsync(2, default))
            .ReturnsAsync(new User { Id = 2, Username = "OtherCoach", Role = UserRole.Trainer });

        // Act
        var result = await _sut.UpdateModerationStatusAsync(request);

        // Assert
        Assert.False(result);
        _userRepoMock.Verify(r => r.UpdateModerationStatusAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateModerationStatusAsync_ValidBlockRequest_Succeeds()
    {
        // Arrange
        var request = new UpdateAthleteModerationRequest
        {
            ModeratorId = 1,
            TargetUserId = 15,
            IsExcludedFromLeaderboard = false,
            IsBlocked = true
        };

        _userRepoMock
            .Setup(r => r.GetByIdAsync(1, default))
            .ReturnsAsync(new User { Id = 1, Username = "ClubAdmin", Role = UserRole.Admin });

        _userRepoMock
            .Setup(r => r.GetByIdAsync(15, default))
            .ReturnsAsync(new User { Id = 15, Username = "SuspectUser", Role = UserRole.User, IsBlocked = false, BlockedAt = null });

        DateTimeOffset? capturedBlockedAt = null;
        _userRepoMock
            .Setup(r => r.UpdateModerationStatusAsync(15, false, true, It.IsAny<DateTimeOffset?>()))
            .Callback<int, bool, bool, DateTimeOffset?>((_, _, _, blockedAt) => capturedBlockedAt = blockedAt)
            .ReturnsAsync(true);

        // Act
        var result = await _sut.UpdateModerationStatusAsync(request);

        // Assert
        Assert.True(result);
        Assert.NotNull(capturedBlockedAt);
        _userRepoMock.Verify(r => r.UpdateModerationStatusAsync(15, false, true, It.IsAny<DateTimeOffset?>()), Times.Once);
    }
}