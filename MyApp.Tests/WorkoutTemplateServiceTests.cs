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

public class WorkoutTemplateServiceTests
{
    private const int TrainerId = 1;

    private readonly Mock<IWorkoutTemplateRepository> _templateRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IExerciseRepository> _exerciseRepositoryMock = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly Mock<ILogger<WorkoutTemplateService>> _loggerMock = new();
    private readonly WorkoutTemplateService _service;

    public WorkoutTemplateServiceTests()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

        _service = new WorkoutTemplateService(
            _templateRepositoryMock.Object,
            _userRepositoryMock.Object,
            _exerciseRepositoryMock.Object,
            _timeProvider,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WhenRequestIsNull_ReturnsValidationFailed()
    {
        // Act
        var result = await _service.CreateAsync(null!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_WhenTrainerIdInvalid_ReturnsValidationFailed(int trainerId)
    {
        // Arrange
        var request = new CreateTemplateRequest(trainerId, "Силове", new[] { new TemplateExerciseDto(10, 3, 10) });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(CreateTemplateRequest.TrainerId), result.Field);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WhenNameEmptyOrWhitespace_ReturnsValidationFailed(string name)
    {
        // Arrange
        var request = new CreateTemplateRequest(TrainerId, name, new[] { new TemplateExerciseDto(10, 3, 10) });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(CreateTemplateRequest.Name), result.Field);
    }

    [Fact]
    public async Task CreateAsync_WhenNameTooLong_ReturnsValidationFailed()
    {
        // Arrange
        var longName = new string('a', TemplateRules.MaxNameLength + 1);
        var request = new CreateTemplateRequest(TrainerId, longName, new[] { new TemplateExerciseDto(10, 3, 10) });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(CreateTemplateRequest.Name), result.Field);
    }

    [Fact]
    public async Task CreateAsync_WhenNoExercises_ReturnsValidationFailed()
    {
        // Arrange
        var request = new CreateTemplateRequest(TrainerId, "Силове", Array.Empty<TemplateExerciseDto>());

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(CreateTemplateRequest.Exercises), result.Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_WhenExerciseIdInvalid_ReturnsValidationFailed(int exerciseId)
    {
        // Arrange
        var request = new CreateTemplateRequest(TrainerId, "Силове", new[] { new TemplateExerciseDto(exerciseId, 3, 10) });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(TemplateExerciseDto.ExerciseId), result.Field);
    }

    [Fact]
    public async Task CreateAsync_WhenExerciseDuplicated_ReturnsValidationFailed()
    {
        // Arrange
        var request = new CreateTemplateRequest(TrainerId, "Силове", new[]
        {
            new TemplateExerciseDto(10, 3, 10),
            new TemplateExerciseDto(10, 4, 8),
        });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(CreateTemplateRequest.Exercises), result.Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(WorkoutRules.MaxSets + 1)]
    public async Task CreateAsync_WhenTargetSetsOutOfRange_ReturnsValidationFailed(int targetSets)
    {
        // Arrange
        var request = new CreateTemplateRequest(TrainerId, "Силове", new[] { new TemplateExerciseDto(10, targetSets, 10) });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(TemplateExerciseDto.TargetSets), result.Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(WorkoutRules.MaxReps + 1)]
    public async Task CreateAsync_WhenTargetRepsOutOfRange_ReturnsValidationFailed(int targetReps)
    {
        // Arrange
        var request = new CreateTemplateRequest(TrainerId, "Силове", new[] { new TemplateExerciseDto(10, 3, targetReps) });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(nameof(TemplateExerciseDto.TargetReps), result.Field);
    }

    [Fact]
    public async Task CreateAsync_WhenUserNotFound_ReturnsUserNotFoundErrorCode()
    {
        // Arrange
        var request = CreateValidRequest();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(TrainerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, result.ErrorCode);
        Assert.Equal(nameof(CreateTemplateRequest.TrainerId), result.Field);
        VerifyTemplateNotSaved();
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Admin)]
    public async Task CreateAsync_WhenUserIsNotTrainer_ReturnsNotATrainerErrorCode(UserRole role)
    {
        // Arrange
        var request = CreateValidRequest();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(TrainerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = TrainerId, Username = "NotCoach", Role = role });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotATrainer, result.ErrorCode);
        Assert.Equal(nameof(CreateTemplateRequest.TrainerId), result.Field);
        VerifyTemplateNotSaved();
    }

    [Fact]
    public async Task CreateAsync_WhenNameAlreadyTaken_ReturnsTemplateNameTakenErrorCode()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupTrainer();
        _templateRepositoryMock
            .Setup(r => r.ExistsByNameAsync(TrainerId, "Силове", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.TemplateNameTaken, result.ErrorCode);
        Assert.Equal(nameof(CreateTemplateRequest.Name), result.Field);
        VerifyTemplateNotSaved();
    }

    [Fact]
    public async Task CreateAsync_WhenExerciseNotFound_ReturnsExerciseNotFoundErrorCode()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupTrainer();
        SetupExercise(10);
        _exerciseRepositoryMock
            .Setup(r => r.GetByIdAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Exercise?)null);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ExerciseNotFound, result.ErrorCode);
        Assert.Equal(nameof(CreateTemplateRequest.Exercises), result.Field);
        VerifyTemplateNotSaved();
    }

    [Fact]
    public async Task CreateAsync_WhenExerciseIsDeleted_ReturnsExerciseNotFoundErrorCode()
    {
        // Arrange
        var request = new CreateTemplateRequest(TrainerId, "Силове", new[] { new TemplateExerciseDto(10, 3, 10) });
        SetupTrainer();
        _exerciseRepositoryMock
            .Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Exercise { Id = 10, Name = "Видалена вправа", IsDeleted = true });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ExerciseNotFound, result.ErrorCode);
        VerifyTemplateNotSaved();
    }

    [Fact]
    public async Task CreateAsync_WhenRequestValid_SavesTemplateWithExercises()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupTrainer();
        SetupExercise(10);
        SetupExercise(11);

        WorkoutTemplate? savedTemplate = null;
        _templateRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<WorkoutTemplate>(), It.IsAny<CancellationToken>()))
            .Callback<WorkoutTemplate, CancellationToken>((t, _) => savedTemplate = t)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(savedTemplate);
        Assert.Equal(TrainerId, savedTemplate.TrainerId);
        Assert.Equal("Силове", savedTemplate.Name);
        Assert.Equal(_timeProvider.GetUtcNow(), savedTemplate.CreatedAt);

        var exercises = savedTemplate.WorkoutTemplateExercises.OrderBy(e => e.Position).ToList();
        Assert.Equal(2, exercises.Count);
        Assert.Equal(10, exercises[0].ExerciseId);
        Assert.Equal((short)1, exercises[0].Position);
        Assert.Equal((short)3, exercises[0].TargetSets);
        Assert.Equal((short)10, exercises[0].TargetReps);
        Assert.Equal(11, exercises[1].ExerciseId);
        Assert.Equal((short)2, exercises[1].Position);
        Assert.Equal((short)4, exercises[1].TargetSets);
        Assert.Equal((short)8, exercises[1].TargetReps);

        _templateRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<WorkoutTemplate>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenNameHasSurroundingSpaces_TrimsNameBeforeCheckAndSave()
    {
        // Arrange
        var request = new CreateTemplateRequest(TrainerId, "  Силове  ", new[] { new TemplateExerciseDto(10, 3, 10) });
        SetupTrainer();
        SetupExercise(10);

        WorkoutTemplate? savedTemplate = null;
        _templateRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<WorkoutTemplate>(), It.IsAny<CancellationToken>()))
            .Callback<WorkoutTemplate, CancellationToken>((t, _) => savedTemplate = t)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(savedTemplate);
        Assert.Equal("Силове", savedTemplate.Name);
        _templateRepositoryMock.Verify(
            r => r.ExistsByNameAsync(TrainerId, "Силове", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenRequestValid_LogsInformation()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupTrainer();
        SetupExercise(10);
        SetupExercise(11);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        VerifyLogged(LogLevel.Information, Times.Once());
        VerifyLogged(LogLevel.Warning, Times.Never());
    }

    [Fact]
    public async Task CreateAsync_WhenRequestRejected_LogsWarning()
    {
        // Arrange
        var request = new CreateTemplateRequest(TrainerId, string.Empty, new[] { new TemplateExerciseDto(10, 3, 10) });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        VerifyLogged(LogLevel.Warning, Times.Once());
        VerifyLogged(LogLevel.Information, Times.Never());
    }

    private static CreateTemplateRequest CreateValidRequest()
    {
        return new CreateTemplateRequest(TrainerId, "Силове", new[]
        {
            new TemplateExerciseDto(10, 3, 10),
            new TemplateExerciseDto(11, 4, 8),
        });
    }

    private void SetupTrainer()
    {
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(TrainerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = TrainerId, Username = "Coach", Role = UserRole.Trainer });
    }

    private void SetupExercise(int exerciseId)
    {
        _exerciseRepositoryMock
            .Setup(r => r.GetByIdAsync(exerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Exercise { Id = exerciseId, Name = $"Вправа {exerciseId}" });
    }

    private void VerifyTemplateNotSaved()
    {
        _templateRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<WorkoutTemplate>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void VerifyLogged(LogLevel level, Times times)
    {
        _loggerMock.Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            times);
    }
}