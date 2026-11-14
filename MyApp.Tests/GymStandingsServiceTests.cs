using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using Xunit;

namespace MyApp.Tests;

public sealed class GymStandingsServiceTests
{
    private const int TrainerId = 2;
    private const int AthleteId = 1;
    private const int KyivRegionId = 1;

    private readonly Mock<IUserRepository> _users = new();

    private readonly FakeLogger<GymStandingsService> _logger = new();

    private readonly GymStandingsService _service;

    public GymStandingsServiceTests()
    {
        _users
            .Setup(u => u.GetByIdAsync(TrainerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = TrainerId, Username = "coach_ivan", Role = UserRole.Trainer });
        _users
            .Setup(u => u.GetByIdAsync(AthleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = AthleteId, Username = "athlete", Role = UserRole.User });
        _users
            .Setup(u => u.GetRegionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRegions());
        _users
            .Setup(u => u.GetGymsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGyms());
        _users
            .Setup(u => u.GetDivisionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Division> { new() { Id = 1, Name = "Новачок" } });
        _users
            .Setup(u => u.GetGymClubStatsAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStats());
        _users
            .Setup(u => u.AddGymAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Gym { Id = 50, RegionId = KyivRegionId, Name = "New Gym" });

        _service = new GymStandingsService(_users.Object, _logger);
    }

    [Fact]
    public async Task GetStandingsAsync_SortsByTotalXpDescendingAndAssignsSequentialRanks()
    {
        var overview = await _service.GetStandingsAsync(AthleteId, null, null);

        Assert.Equal(new[] { 1, 2, 3 }, overview.Standings.Select(s => s.Rank));
        Assert.Equal(new[] { "Iron Temple", "Alpha Gym", "Empty Club" }, overview.Standings.Select(s => s.GymName));
    }

    [Fact]
    public async Task GetStandingsAsync_CalculatesAverageXpPerAthlete()
    {
        var overview = await _service.GetStandingsAsync(AthleteId, null, null);

        var iron = overview.Standings.Single(s => s.GymName == "Iron Temple");
        var alpha = overview.Standings.Single(s => s.GymName == "Alpha Gym");
        var empty = overview.Standings.Single(s => s.GymName == "Empty Club");
        Assert.Equal(1000, iron.AverageXp);
        Assert.Equal(1000, alpha.AverageXp);
        Assert.Equal(0, empty.AverageXp);
    }

    [Theory]
    [InlineData(1000, 3, 333)]
    [InlineData(5, 2, 3)]
    [InlineData(7, 1, 7)]
    public async Task GetStandingsAsync_RoundsAverageXpToNearestWholeNumber(long totalXp, int athletes, long expected)
    {
        _users
            .Setup(u => u.GetGymClubStatsAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GymClubStatsDto> { new(1, "Solo", 1, "Київ", athletes, totalXp, "a") });

        var overview = await _service.GetStandingsAsync(AthleteId, null, null);

        Assert.Equal(expected, overview.Standings.Single().AverageXp);
    }

    [Fact]
    public async Task GetStandingsAsync_WhenGymHasNoLeader_ShowsDash()
    {
        var overview = await _service.GetStandingsAsync(AthleteId, null, null);

        Assert.Equal("max_power", overview.Standings[0].LeaderNickname);
        Assert.Equal("—", overview.Standings[2].LeaderNickname);
    }

    [Fact]
    public async Task GetStandingsAsync_WhenTotalXpIsEqual_PrefersMoreAthletesThenName()
    {
        _users
            .Setup(u => u.GetGymClubStatsAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GymClubStatsDto>
            {
                new(1, "Beta", 1, "Київ", 1, 500, "a"),
                new(2, "Alpha", 1, "Київ", 2, 500, "b"),
                new(3, "Aardvark", 1, "Київ", 1, 500, "c"),
            });

        var overview = await _service.GetStandingsAsync(AthleteId, null, null);

        Assert.Equal(new[] { "Alpha", "Aardvark", "Beta" }, overview.Standings.Select(s => s.GymName));
    }

    [Fact]
    public async Task GetStandingsAsync_PassesFiltersToRepository()
    {
        await _service.GetStandingsAsync(AthleteId, KyivRegionId, 3);

        _users.Verify(
            u => u.GetGymClubStatsAsync(KyivRegionId, 3, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStandingsAsync_WhenDivisionSelected_HidesGymsWithoutAthletes()
    {
        var overview = await _service.GetStandingsAsync(AthleteId, null, 1);

        Assert.DoesNotContain(overview.Standings, s => s.GymName == "Empty Club");
        Assert.Equal(2, overview.Standings.Count);
    }

    [Fact]
    public async Task GetStandingsAsync_WhenNoDivisionSelected_KeepsGymsWithoutAthletes()
    {
        var overview = await _service.GetStandingsAsync(AthleteId, null, null);

        Assert.Contains(overview.Standings, s => s.GymName == "Empty Club");
    }

    [Fact]
    public async Task GetStandingsAsync_ReturnsSortedRegionsAndDivisions()
    {
        var overview = await _service.GetStandingsAsync(AthleteId, null, null);

        Assert.Equal(new[] { "Київ", "Львівська область" }, overview.Regions.Select(r => r.Name));
        Assert.Single(overview.Divisions);
    }

    [Theory]
    [InlineData(TrainerId, true)]
    [InlineData(AthleteId, false)]
    [InlineData(99, false)]
    [InlineData(0, false)]
    public async Task GetStandingsAsync_SetsCanRegisterGymsByRole(int userId, bool expected)
    {
        var overview = await _service.GetStandingsAsync(userId, null, null);

        Assert.Equal(expected, overview.CanRegisterGyms);
    }

    [Fact]
    public async Task RegisterGymAsync_WhenTrainerSendsValidRequest_CreatesGymAndLogsInformation()
    {
        var result = await _service.RegisterGymAsync(new RegisterGymRequest(TrainerId, KyivRegionId, "  New Gym  "));

        Assert.True(result.IsSuccess);
        _users.Verify(
            u => u.AddGymAsync(KyivRegionId, "New Gym", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
        Assert.DoesNotContain(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task RegisterGymAsync_WhenAdminSendsValidRequest_CreatesGym()
    {
        _users
            .Setup(u => u.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 3, Username = "admin", Role = UserRole.Admin });

        var result = await _service.RegisterGymAsync(new RegisterGymRequest(3, KyivRegionId, "New Gym"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RegisterGymAsync_WhenUserHasRoleUser_ReturnsAccessDeniedAndDoesNotSave()
    {
        var result = await _service.RegisterGymAsync(new RegisterGymRequest(AthleteId, KyivRegionId, "New Gym"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ModerationAccessDenied, result.ErrorCode);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        _users.Verify(
            u => u.AddGymAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterGymAsync_WhenUserNotFound_ReturnsAccessDenied()
    {
        var result = await _service.RegisterGymAsync(new RegisterGymRequest(99, KyivRegionId, "New Gym"));

        Assert.Equal(ErrorCodes.ModerationAccessDenied, result.ErrorCode);
        _users.Verify(
            u => u.AddGymAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("  ab  ")]
    public async Task RegisterGymAsync_WhenNameEmptyOrTooShort_ReturnsValidationFailedForName(string name)
    {
        var result = await _service.RegisterGymAsync(new RegisterGymRequest(TrainerId, KyivRegionId, name));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(RegisterGymRequest.Name), result.Field);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        _users.Verify(
            u => u.AddGymAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterGymAsync_WhenNameLongerThan150_ReturnsValidationFailedForName()
    {
        var result = await _service.RegisterGymAsync(new RegisterGymRequest(TrainerId, KyivRegionId, new string('x', 151)));

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(RegisterGymRequest.Name), result.Field);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(150)]
    public async Task RegisterGymAsync_WhenNameOnLengthBoundary_Succeeds(int length)
    {
        var result = await _service.RegisterGymAsync(new RegisterGymRequest(TrainerId, KyivRegionId, new string('x', length)));

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task RegisterGymAsync_WhenRegionNotChosen_ReturnsValidationFailedForRegionId(int regionId)
    {
        var result = await _service.RegisterGymAsync(new RegisterGymRequest(TrainerId, regionId, "New Gym"));

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(RegisterGymRequest.RegionId), result.Field);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task RegisterGymAsync_WhenRegionDoesNotExist_ReturnsRegionNotFound()
    {
        var result = await _service.RegisterGymAsync(new RegisterGymRequest(TrainerId, 777, "New Gym"));

        Assert.Equal(ErrorCodes.RegionNotFound, result.ErrorCode);
        Assert.Equal(nameof(RegisterGymRequest.RegionId), result.Field);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        _users.Verify(
            u => u.AddGymAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("Iron Temple")]
    [InlineData("  iron temple  ")]
    [InlineData("IRON TEMPLE")]
    public async Task RegisterGymAsync_WhenNameExistsInSameRegion_ReturnsGymNameTaken(string name)
    {
        var result = await _service.RegisterGymAsync(new RegisterGymRequest(TrainerId, KyivRegionId, name));

        Assert.Equal(ErrorCodes.GymNameTaken, result.ErrorCode);
        Assert.Equal(nameof(RegisterGymRequest.Name), result.Field);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        _users.Verify(
            u => u.AddGymAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterGymAsync_WhenNameExistsOnlyInAnotherRegion_Succeeds()
    {
        var result = await _service.RegisterGymAsync(new RegisterGymRequest(TrainerId, 2, "Iron Temple"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RegisterGymAsync_WhenRequestIsNull_ReturnsValidationFailed()
    {
        var result = await _service.RegisterGymAsync(null!);

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        _users.Verify(
            u => u.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static List<Region> CreateRegions()
    {
        return new List<Region>
        {
            new() { Id = 2, Name = "Львівська область" },
            new() { Id = KyivRegionId, Name = "Київ" },
        };
    }

    private static List<Gym> CreateGyms()
    {
        return new List<Gym>
        {
            new() { Id = 1, RegionId = KyivRegionId, Name = "Iron Temple" },
            new() { Id = 2, RegionId = KyivRegionId, Name = "Alpha Gym" },
            new() { Id = 3, RegionId = 2, Name = "Empty Club" },
        };
    }

    private static List<GymClubStatsDto> CreateStats()
    {
        return new List<GymClubStatsDto>
        {
            new(2, "Alpha Gym", KyivRegionId, "Київ", 3, 3000, "zeta"),
            new(3, "Empty Club", 2, "Львівська область", 0, 0, null),
            new(1, "Iron Temple", KyivRegionId, "Київ", 4, 4000, "max_power"),
        };
    }
}
