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

public sealed class UserProfileServiceTests
{
    private readonly Mock<IUserRepository> _users = new();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));

    private readonly FakeLogger<UserProfileService> _logger = new();

    private readonly UserProfileService _service;

    public UserProfileServiceTests()
    {
        _users
            .Setup(u => u.UpdateProfileAsync(
                It.IsAny<int>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _service = new UserProfileService(_users.Object, _time, _logger);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_SavesAndSucceeds()
    {
        var result = await _service.UpdateAsync(new UpdateProfileRequest(1, 80m, 180m, new DateOnly(1995, 5, 5)));

        Assert.True(result.IsSuccess);
        _users.Verify(
            u => u.UpdateProfileAsync(1, 80m, 180m, new DateOnly(1995, 5, 5), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_PartialRequest_PassesNullsForMissingFields()
    {
        var result = await _service.UpdateAsync(new UpdateProfileRequest(1, 80m, null, null));

        Assert.True(result.IsSuccess);
        _users.Verify(
            u => u.UpdateProfileAsync(1, 80m, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_LogsInformationWithoutValues()
    {
        await _service.UpdateAsync(new UpdateProfileRequest(1, 80m, 180m, new DateOnly(1995, 5, 5)));

        var record = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
        Assert.Equal("Profile updated for 1", record.Message);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(500)]
    public async Task UpdateAsync_BoundaryWeight_Succeeds(int weight)
    {
        var result = await _service.UpdateAsync(new UpdateProfileRequest(1, weight, null, null));

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(272)]
    public async Task UpdateAsync_BoundaryHeight_Succeeds(int height)
    {
        var result = await _service.UpdateAsync(new UpdateProfileRequest(1, null, height, null));

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(2016, 10, 18)]
    [InlineData(1906, 10, 18)]
    public async Task UpdateAsync_BoundaryAge_Succeeds(int year, int month, int day)
    {
        var result = await _service.UpdateAsync(new UpdateProfileRequest(1, null, null, new DateOnly(year, month, day)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetAsync_ExistingUser_ReturnsProfile()
    {
        _users
            .Setup(u => u.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser());

        var profile = await _service.GetAsync(1);

        Assert.Equal(new UserProfileDto(1, 80m, 180m, new DateOnly(1995, 5, 5)), profile);
    }

    [Fact]
    public async Task GetAsync_UserNotFound_ReturnsNull()
    {
        _users
            .Setup(u => u.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var profile = await _service.GetAsync(99);

        Assert.Null(profile);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetAsync_InvalidUserId_ReturnsNullWithoutCallingRepository(int userId)
    {
        var profile = await _service.GetAsync(userId);

        Assert.Null(profile);
        _users.Verify(u => u.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_NullRequest_ReturnsValidationFailed()
    {
        var result = await _service.UpdateAsync(null!);

        AssertRejected(result, "request");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateAsync_InvalidUserId_ReturnsValidationFailed(int userId)
    {
        var result = await _service.UpdateAsync(new UpdateProfileRequest(userId, 80m, null, null));

        AssertRejected(result, nameof(UpdateProfileRequest.UserId));
    }

    [Theory]
    [InlineData(19)]
    [InlineData(501)]
    public async Task UpdateAsync_WeightOutOfRange_ReturnsValidationFailed(int weight)
    {
        var result = await _service.UpdateAsync(new UpdateProfileRequest(1, weight, null, null));

        AssertRejected(result, nameof(UpdateProfileRequest.WeightKg));
    }

    [Theory]
    [InlineData(49)]
    [InlineData(273)]
    public async Task UpdateAsync_HeightOutOfRange_ReturnsValidationFailed(int height)
    {
        var result = await _service.UpdateAsync(new UpdateProfileRequest(1, null, height, null));

        AssertRejected(result, nameof(UpdateProfileRequest.HeightCm));
    }

    [Fact]
    public async Task UpdateAsync_BirthDateInFuture_ReturnsValidationFailed()
    {
        var result = await _service.UpdateAsync(new UpdateProfileRequest(1, null, null, new DateOnly(2026, 10, 19)));

        AssertRejected(result, nameof(UpdateProfileRequest.BirthDate));
    }

    [Theory]
    [InlineData(2016, 10, 19)]
    [InlineData(1905, 10, 18)]
    public async Task UpdateAsync_AgeOutOfRange_ReturnsValidationFailed(int year, int month, int day)
    {
        var result = await _service.UpdateAsync(new UpdateProfileRequest(1, null, null, new DateOnly(year, month, day)));

        AssertRejected(result, nameof(UpdateProfileRequest.BirthDate));
    }

    [Fact]
    public async Task UpdateAsync_AllFieldsEmpty_ReturnsValidationFailed()
    {
        var result = await _service.UpdateAsync(new UpdateProfileRequest(1, null, null, null));

        AssertRejected(result, "Profile");
    }

    [Fact]
    public async Task UpdateAsync_InvalidData_DoesNotLogValues()
    {
        await _service.UpdateAsync(new UpdateProfileRequest(1, 501m, null, null));

        var record = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.DoesNotContain("501", record.Message);
    }

    [Fact]
    public async Task UpdateAsync_UserNotFound_ReturnsUserNotFound()
    {
        _users
            .Setup(u => u.UpdateProfileAsync(
                99,
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.UpdateAsync(new UpdateProfileRequest(99, 80m, null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, result.ErrorCode);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.DoesNotContain(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
    }

    private static User CreateUser()
    {
        return new User
        {
            Id = 1,
            WeightKg = 80m,
            HeightCm = 180m,
            BirthDate = new DateTime(1995, 5, 5, 0, 0, 0, DateTimeKind.Utc),
        };
    }

    private void AssertRejected(OperationResult result, string field)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(field, result.Field);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);

        _users.Verify(
            u => u.UpdateProfileAsync(
                It.IsAny<int>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}