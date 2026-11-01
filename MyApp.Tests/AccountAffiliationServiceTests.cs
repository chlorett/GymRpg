using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Rules;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using Xunit;

namespace MyApp.Tests;

public sealed class AccountAffiliationServiceTests
{
    private const int UserId = 7;
    private const string OwnUsername = "denis_rusyn";

    private readonly Mock<IUserRepository> _users = new();

    private readonly FakeLogger<AccountAffiliationService> _logger = new();

    private readonly AccountAffiliationService _service;

    public AccountAffiliationServiceTests()
    {
        _users
            .Setup(u => u.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(gymId: 2, divisionId: 1));
        _users
            .Setup(u => u.GetRegionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRegions());
        _users
            .Setup(u => u.GetGymsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGyms());
        _users
            .Setup(u => u.GetDivisionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDivisions());
        _users
            .Setup(u => u.CountByGymAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _users
            .Setup(u => u.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _users
            .Setup(u => u.UpdateAffiliationAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _service = new AccountAffiliationService(_users.Object, _logger);
    }

    [Fact]
    public async Task GetAffiliationDataAsync_WhenUserExists_ReturnsAffiliationAndReferenceData()
    {
        var data = await _service.GetAffiliationDataAsync(UserId);

        Assert.NotNull(data);
        Assert.Equal(UserId, data.UserId);
        Assert.Equal(OwnUsername, data.Username);
        Assert.Equal(2, data.RegionId);
        Assert.Equal(2, data.GymId);
        Assert.Equal(1, data.DivisionId);
        Assert.Equal(5, data.GymMembersCount);
        Assert.Equal(new[] { "Київ", "Львівська область" }, data.Regions.Select(r => r.Name));
        Assert.Equal(3, data.Gyms.Count);
        Assert.Equal(2, data.Divisions.Count);
    }

    [Fact]
    public async Task GetAffiliationDataAsync_WhenUserHasNoGym_ReturnsEmptyAffiliationWithoutCounting()
    {
        _users
            .Setup(u => u.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(gymId: null, divisionId: null));

        var data = await _service.GetAffiliationDataAsync(UserId);

        Assert.NotNull(data);
        Assert.Null(data.RegionId);
        Assert.Null(data.GymId);
        Assert.Null(data.DivisionId);
        Assert.Equal(0, data.GymMembersCount);
        Assert.Equal(2, data.Regions.Count);
        _users.Verify(u => u.CountByGymAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAffiliationDataAsync_WhenUserNotFound_ReturnsNullAndLogsWarning()
    {
        _users
            .Setup(u => u.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var data = await _service.GetAffiliationDataAsync(UserId);

        Assert.Null(data);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetAffiliationDataAsync_WhenUserIdInvalid_ReturnsNullWithoutLoading(int userId)
    {
        var data = await _service.GetAffiliationDataAsync(userId);

        Assert.Null(data);
        _users.Verify(u => u.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAffiliationAsync_WhenRequestValid_SavesTrimmedUsernameGymAndDivision()
    {
        var result = await _service.UpdateAffiliationAsync(CreateRequest(username: "  new_name ", gymId: 3, divisionId: 2));

        Assert.True(result.IsSuccess);
        _users.Verify(
            u => u.UpdateAffiliationAsync(UserId, "new_name", 3, 2, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
    }

    [Fact]
    public async Task UpdateAffiliationAsync_WhenGymAndDivisionNotChosen_ClearsAffiliation()
    {
        var result = await _service.UpdateAffiliationAsync(CreateRequest(gymId: null, divisionId: null));

        Assert.True(result.IsSuccess);
        _users.Verify(
            u => u.UpdateAffiliationAsync(UserId, OwnUsername, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAffiliationAsync_WhenUsernameDiffersOnlyByCaseAndBelongsToSameUser_Succeeds()
    {
        _users
            .Setup(u => u.GetByUsernameAsync("Denis_Rusyn", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(gymId: 2, divisionId: 1));

        var result = await _service.UpdateAffiliationAsync(CreateRequest(username: "Denis_Rusyn"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateAffiliationAsync_WhenUsernameTakenByAnotherUser_ReturnsUsernameTaken()
    {
        _users
            .Setup(u => u.GetByUsernameAsync("taken_name", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 99, Username = "taken_name" });

        var result = await _service.UpdateAffiliationAsync(CreateRequest(username: "taken_name"));

        AssertFailed(result, ErrorCodes.UsernameTaken, nameof(UpdateAffiliationRequest.Username));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("bad name")]
    [InlineData("денис")]
    public async Task UpdateAffiliationAsync_WhenUsernameInvalid_ReturnsValidationFailed(string username)
    {
        var result = await _service.UpdateAffiliationAsync(CreateRequest(username: username));

        AssertFailed(result, ErrorCodes.ValidationFailed, nameof(UpdateAffiliationRequest.Username));
        _users.Verify(u => u.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAffiliationAsync_WhenUsernameTooLong_ReturnsValidationFailed()
    {
        var result = await _service.UpdateAffiliationAsync(
            CreateRequest(username: new string('a', AuthRules.MaxUsernameLength + 1)));

        AssertFailed(result, ErrorCodes.ValidationFailed, nameof(UpdateAffiliationRequest.Username));
    }

    [Fact]
    public async Task UpdateAffiliationAsync_WhenGymDoesNotExist_ReturnsGymNotFound()
    {
        var result = await _service.UpdateAffiliationAsync(CreateRequest(gymId: 99));

        AssertFailed(result, ErrorCodes.GymNotFound, nameof(UpdateAffiliationRequest.GymId));
    }

    [Fact]
    public async Task UpdateAffiliationAsync_WhenDivisionDoesNotExist_ReturnsDivisionNotFound()
    {
        var result = await _service.UpdateAffiliationAsync(CreateRequest(divisionId: 99));

        AssertFailed(result, ErrorCodes.DivisionNotFound, nameof(UpdateAffiliationRequest.DivisionId));
    }

    [Fact]
    public async Task UpdateAffiliationAsync_WhenUserDoesNotExist_ReturnsUserNotFound()
    {
        _users
            .Setup(u => u.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _service.UpdateAffiliationAsync(CreateRequest());

        AssertFailed(result, ErrorCodes.UserNotFound, nameof(UpdateAffiliationRequest.UserId));
    }

    [Fact]
    public async Task UpdateAffiliationAsync_WhenRepositoryDidNotUpdateUser_ReturnsUserNotFound()
    {
        _users
            .Setup(u => u.UpdateAffiliationAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.UpdateAffiliationAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, result.ErrorCode);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task UpdateAffiliationAsync_WhenRequestIsNull_ReturnsValidationFailed()
    {
        var result = await _service.UpdateAffiliationAsync(null!);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("request", result.Field);
    }

    private static UpdateAffiliationRequest CreateRequest(
        string username = OwnUsername,
        int? gymId = 2,
        int? divisionId = 1)
    {
        return new UpdateAffiliationRequest(UserId, username, gymId, divisionId);
    }

    private static User CreateUser(int? gymId, int? divisionId)
    {
        return new User
        {
            Id = UserId,
            Email = "denis@example.com",
            Username = OwnUsername,
            GymId = gymId,
            DivisionId = divisionId,
        };
    }

    private static IReadOnlyList<Region> CreateRegions()
    {
        return new List<Region>
        {
            new() { Id = 2, Name = "Львівська область" },
            new() { Id = 1, Name = "Київ" },
        };
    }

    private static IReadOnlyList<Gym> CreateGyms()
    {
        return new List<Gym>
        {
            new() { Id = 1, RegionId = 1, Name = "Iron Temple Київ" },
            new() { Id = 2, RegionId = 2, Name = "Gym Lviv Strong" },
            new() { Id = 3, RegionId = 2, Name = "Львів Фітнес Арена" },
        };
    }

    private static IReadOnlyList<Division> CreateDivisions()
    {
        return new List<Division>
        {
            new() { Id = 1, Name = "Новачок" },
            new() { Id = 2, Name = "Любитель" },
        };
    }

    private void AssertFailed(OperationResult result, string expectedCode, string expectedField)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(expectedField, result.Field);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.DoesNotContain(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);

        _users.Verify(
            u => u.UpdateAffiliationAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
