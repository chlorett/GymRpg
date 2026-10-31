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

    [Fact]
    public async Task GetUserTemplatesAsync_ReturnsSortedSummariesAndFiltersBySearchQuery()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var templates = new List<WorkoutTemplate>
        {
            new()
            {
                Id = 1,
                TrainerId = TrainerId,
                Name = "Базовий жим і спина",
                Description = "Верх тіла",
                CreatedAt = now.AddDays(-2),
                WorkoutTemplateExercises = new List<WorkoutTemplateExercise>
                {
                    new() { ExerciseId = 1, Position = 1, TargetSets = 4, TargetReps = 8 },
                    new() { ExerciseId = 2, Position = 2, TargetSets = 3, TargetReps = 10 },
                },
            },
            new()
            {
                Id = 2,
                TrainerId = TrainerId,
                Name = "День ніг та плечей",
                Description = "Присідання та жим стоячи",
                CreatedAt = now.AddDays(-1),
                WorkoutTemplateExercises = new List<WorkoutTemplateExercise>
                {
                    new() { ExerciseId = 3, Position = 1, TargetSets = 5, TargetReps = 5 },
                },
            },
        };

        _templateRepositoryMock
            .Setup(r => r.GetByUserIdAsync(TrainerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Act
        var allResult = await _service.GetUserTemplatesAsync(TrainerId);
        var filteredResult = await _service.GetUserTemplatesAsync(TrainerId, "  БАЗОВИЙ ");

        // Assert
        Assert.Equal(2, allResult.Count);
        Assert.Equal("День ніг та плечей", allResult[0].Name);
        Assert.Equal("Базовий жим і спина", allResult[1].Name);
        Assert.Equal(2, allResult[1].ExercisesCount);
        Assert.Equal(7, allResult[1].TotalSetsCount);
        Assert.Equal(62, allResult[1].TotalVolumeReps);

        Assert.Single(filteredResult);
        Assert.Equal("Базовий жим і спина", filteredResult[0].Name);
    }

    [Fact]
    public async Task GetTemplateDetailsAsync_OrdersExercisesByPositionAndComputesVolume()
    {
        // Arrange
        var template = new WorkoutTemplate
        {
            Id = 5,
            TrainerId = TrainerId,
            Name = "Фулбоді",
            Description = "Комплексна програма",
            CreatedAt = _timeProvider.GetUtcNow(),
            WorkoutTemplateExercises = new List<WorkoutTemplateExercise>
            {
                new()
                {
                    ExerciseId = 4,
                    Position = 2,
                    TargetSets = 3,
                    TargetReps = 12,
                    Exercise = new Exercise { Id = 4, Name = "Підтягування", EquipmentType = EquipmentType.Calisthenics },
                },
                new()
                {
                    ExerciseId = 1,
                    Position = 1,
                    TargetSets = 4,
                    TargetReps = 8,
                    Exercise = new Exercise { Id = 1, Name = "Жим лежачи", EquipmentType = EquipmentType.Gym },
                },
            },
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdWithExercisesAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var details = await _service.GetTemplateDetailsAsync(templateId: 5, userId: TrainerId);

        // Assert
        Assert.NotNull(details);
        Assert.Equal("Фулбоді", details.Name);
        Assert.Equal(2, details.ExercisesCount);
        Assert.Equal(7, details.TotalSetsCount);
        Assert.Equal(68, details.TotalVolumeReps);
        Assert.Equal(1, details.Exercises[0].Position);
        Assert.Equal("Жим лежачи", details.Exercises[0].ExerciseName);
        Assert.Equal("4 × 8", details.Exercises[0].SchemeText);
        Assert.Equal(2, details.Exercises[1].Position);
        Assert.Equal("Підтягування", details.Exercises[1].ExerciseName);
    }

    [Fact]
    public async Task DeleteTemplateAsync_WhenOwnerDeletesOwnTemplate_ReturnsSuccess()
    {
        // Arrange
        var template = new WorkoutTemplate
        {
            Id = 7,
            TrainerId = TrainerId,
            Name = "Старий шаблон",
        };
        _templateRepositoryMock
            .Setup(r => r.GetByIdWithExercisesAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _service.DeleteTemplateAsync(templateId: 7, userId: TrainerId);

        // Assert
        Assert.True(result.IsSuccess);
        _templateRepositoryMock.Verify(
            r => r.DeleteAsync(7, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteTemplateAsync_WhenTemplateDoesNotExist_ReturnsTemplateNotFoundError()
    {
        // Arrange
        _templateRepositoryMock
            .Setup(r => r.GetByIdWithExercisesAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkoutTemplate?)null);

        // Act
        var result = await _service.DeleteTemplateAsync(templateId: 99, userId: TrainerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.TemplateNotFound, result.ErrorCode);
        _templateRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteTemplateAsync_WhenUserIsNotOwner_ReturnsTemplateAccessDeniedError()
    {
        // Arrange
        var template = new WorkoutTemplate
        {
            Id = 8,
            TrainerId = 2,
            Name = "Чужий шаблон",
        };
        _templateRepositoryMock
            .Setup(r => r.GetByIdWithExercisesAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _service.DeleteTemplateAsync(templateId: 8, userId: TrainerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.TemplateAccessDenied, result.ErrorCode);
        _templateRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
