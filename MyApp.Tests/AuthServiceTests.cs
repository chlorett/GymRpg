using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Rules;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Tests;

public class AuthServiceTests
{
    private const string ValidPass = "Strong123";

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly Mock<ILogger<AuthService>> _loggerMock = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        _hasherMock
            .Setup(h => h.Hash(It.IsAny<string>()))
            .Returns("hashed-value");

        _service = new AuthService(
            _userRepositoryMock.Object,
            _hasherMock.Object,
            _timeProvider,
            _loggerMock.Object);
    }

    [Fact]
    public async Task RegisterAsync_WhenRequestIsNull_ReturnsValidationFailed()
    {
        // Act
        var result = await _service.RegisterAsync(null!);

        // Assert
        AssertValidationFailed(result, "request");
    }

    [Fact]
    public async Task RegisterAsync_WhenRequestValid_SavesNormalizedUserAndReturnsSuccess()
    {
        // Arrange
        var request = CreateRequest(email: "  Denis@Example.COM ", username: "  denis_rusyn ");

        User? savedUser = null;
        _userRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => savedUser = u)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.RegisterAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(savedUser);
        Assert.Equal("denis@example.com", savedUser.Email);
        Assert.Equal("denis_rusyn", savedUser.Username);
        Assert.Equal(UserRole.User, savedUser.Role);
        Assert.Equal(80m, savedUser.WeightKg);
        Assert.Equal(180m, savedUser.HeightCm);
        Assert.Equal(new DateTime(1995, 5, 20, 0, 0, 0, DateTimeKind.Utc), savedUser.BirthDate);
        Assert.Equal(_timeProvider.GetUtcNow(), savedUser.CreatedAt);
        Assert.Equal(_timeProvider.GetUtcNow(), savedUser.UpdatedAt);
    }

    [Fact]
    public async Task RegisterAsync_WhenRequestValid_StoresHashInsteadOfPlainText()
    {
        // Arrange
        User? savedUser = null;
        _userRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => savedUser = u)
            .Returns(Task.CompletedTask);

        // Act
        await _service.RegisterAsync(CreateRequest());

        // Assert
        Assert.NotNull(savedUser);
        Assert.Equal("hashed-value", savedUser.PasswordHash);
        Assert.NotEqual(ValidPass, savedUser.PasswordHash);
        _hasherMock.Verify(h => h.Hash(ValidPass), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailTaken_ReturnsEmailTakenCaseInsensitively()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync("denis@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 7, Email = "denis@example.com" });

        // Act
        var result = await _service.RegisterAsync(CreateRequest(email: "Denis@Example.com"));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.EmailTaken, result.ErrorCode);
        Assert.Equal(nameof(RegisterRequest.Email), result.Field);
        _userRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenUsernameTaken_ReturnsUsernameTaken()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.GetByUsernameAsync("denis_rusyn", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 7, Username = "Denis_Rusyn" });

        // Act
        var result = await _service.RegisterAsync(CreateRequest(username: " denis_rusyn "));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UsernameTaken, result.ErrorCode);
        Assert.Equal(nameof(RegisterRequest.Username), result.Field);
        _userRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("bad name")]
    [InlineData("denis-rusyn")]
    [InlineData("денис")]
    public async Task RegisterAsync_WhenUsernameInvalid_ReturnsValidationFailed(string username)
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(username: username));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.Username));
    }

    [Fact]
    public async Task RegisterAsync_WhenUsernameTooLong_ReturnsValidationFailed()
    {
        // Arrange
        var username = new string('a', AuthRules.MaxUsernameLength + 1);

        // Act
        var result = await _service.RegisterAsync(CreateRequest(username: username));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.Username));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("user@host")]
    [InlineData("a b@example.com")]
    [InlineData("Denis <denis@example.com>")]
    public async Task RegisterAsync_WhenEmailInvalid_ReturnsValidationFailed(string email)
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(email: email));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.Email));
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailTooLong_ReturnsValidationFailed()
    {
        // Arrange
        var email = new string('a', AuthRules.MaxEmailLength) + "@example.com";

        // Act
        var result = await _service.RegisterAsync(CreateRequest(email: email));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.Email));
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("short1")]
    public async Task RegisterAsync_WhenPasswordTooShort_ReturnsValidationFailed(string pass)
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(pass: pass));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.Password));
    }

    [Fact]
    public async Task RegisterAsync_WhenPasswordExceedsBcryptLimit_ReturnsValidationFailed()
    {
        // Arrange
        var pass = new string('a', AuthRules.MaxPasswordBytes + 1);

        // Act
        var result = await _service.RegisterAsync(CreateRequest(pass: pass));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.Password));
    }

    [Fact]
    public async Task RegisterAsync_WhenCyrillicPasswordExceedsBcryptLimitInBytes_ReturnsValidationFailed()
    {
        // Arrange: 37 кириличних символів займають 74 байти, хоча символів менше за 72
        var pass = new string('я', 37);

        // Act
        var result = await _service.RegisterAsync(CreateRequest(pass: pass));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.Password));
    }

    [Fact]
    public async Task RegisterAsync_WhenPasswordsDoNotMatch_ReturnsValidationFailed()
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(confirm: "Different123"));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.ConfirmPassword));
    }

    [Fact]
    public async Task RegisterAsync_WhenWeightBelowMinimum_ReturnsValidationFailed()
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(weightKg: AuthRules.MinWeightKg - 0.1m));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.WeightKg));
    }

    [Fact]
    public async Task RegisterAsync_WhenWeightAboveMaximum_ReturnsValidationFailed()
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(weightKg: AuthRules.MaxWeightKg + 0.1m));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.WeightKg));
    }

    [Fact]
    public async Task RegisterAsync_WhenHeightBelowMinimum_ReturnsValidationFailed()
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(heightCm: AuthRules.MinHeightCm - 0.1m));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.HeightCm));
    }

    [Fact]
    public async Task RegisterAsync_WhenHeightAboveMaximum_ReturnsValidationFailed()
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(heightCm: AuthRules.MaxHeightCm + 0.1m));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.HeightCm));
    }

    [Fact]
    public async Task RegisterAsync_WhenBirthDateInFuture_ReturnsValidationFailed()
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(birthDate: new DateOnly(2026, 10, 12)));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.BirthDate));
    }

    [Fact]
    public async Task RegisterAsync_WhenUserTurnsMinimumAgeTomorrow_ReturnsValidationFailed()
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(birthDate: new DateOnly(2013, 10, 12)));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.BirthDate));
    }

    [Fact]
    public async Task RegisterAsync_WhenUserTurnsMinimumAgeToday_ReturnsSuccess()
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(birthDate: new DateOnly(2013, 10, 11)));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RegisterAsync_WhenBirthDateNotSelected_ReturnsValidationFailed()
    {
        // Act
        var result = await _service.RegisterAsync(CreateRequest(birthDate: default(DateOnly)));

        // Assert
        AssertValidationFailed(result, nameof(RegisterRequest.BirthDate));
    }

    private static RegisterRequest CreateRequest(
        string email = "denis@example.com",
        string username = "denis_rusyn",
        string pass = ValidPass,
        string? confirm = null,
        decimal weightKg = 80m,
        decimal heightCm = 180m,
        DateOnly? birthDate = null)
    {
        return new RegisterRequest(
            email,
            username,
            pass,
            confirm ?? pass,
            weightKg,
            heightCm,
            birthDate ?? new DateOnly(1995, 5, 20));
    }

    private void AssertValidationFailed(OperationResult result, string expectedField)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(expectedField, result.Field);

        _userRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }
}
