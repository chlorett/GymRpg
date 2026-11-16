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

public class ExerciseCatalogServiceTests
{
    private static readonly Exercise[] Data =
    {
        new() { Id = 1, Name = "Жим лежачи на лаві", EquipmentType = EquipmentType.Gym, Description = "Груди та трицепс", Technique = "Техніка 1" },
        new() { Id = 2, Name = "Присідання зі штангою", EquipmentType = EquipmentType.Gym, Description = "Ноги та сідниці", Technique = "Техніка 2" },
        new() { Id = 3, Name = "Підтягування на перекладині", EquipmentType = EquipmentType.Calisthenics, Description = "Спина та біцепс", Technique = "Техніка 3" },
        new() { Id = 4, Name = "Віджимання на брусах", EquipmentType = EquipmentType.Calisthenics, Description = "Груди та трицепс", Technique = "Техніка 4" },
    };

    private readonly FakeLogger<ExerciseCatalogService> logger = new();
    private readonly FakeLogger<ExerciseCatalogService> managementLogger = new();
    private readonly Mock<IExerciseRepository> managementRepository = new();
    private readonly ExerciseCatalogService service;

    public ExerciseCatalogServiceTests()
    {
        var repository = new Mock<IExerciseRepository>();
        repository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Data);
        this.service = new ExerciseCatalogService(repository.Object, this.logger);
    }

    [Fact]
    public async Task GetExercisesAsync_NoFilters_ReturnsAllExercises()
    {
        var result = await this.service.GetExercisesAsync(null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.Count);
    }

    [Theory]
    [InlineData("ЖИМ ЛЕЖАЧИ")]
    [InlineData("жим лежачи")]
    [InlineData("  Жим лежачи  ")]
    public async Task GetExercisesAsync_SearchByName_IgnoresCase(string query)
    {
        var result = await this.service.GetExercisesAsync(query, null);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!);
        Assert.Equal("Жим лежачи на лаві", item.Name);
    }

    [Fact]
    public async Task GetExercisesAsync_FilterByEquipmentType_ReturnsOnlyMatching()
    {
        var result = await this.service.GetExercisesAsync(null, EquipmentType.Calisthenics);

        Assert.True(result.IsSuccess);
        var items = result.Value!;
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(EquipmentType.Calisthenics, i.EquipmentType));
        Assert.All(items, i => Assert.Equal("Калістеніка / Власна вага", i.EquipmentLabel));
    }

    [Fact]
    public async Task GetExercisesAsync_SearchAndEquipmentFilter_AreCombined()
    {
        var result = await this.service.GetExercisesAsync("трицепс", EquipmentType.Calisthenics);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!);
        Assert.Equal("Віджимання на брусах", item.Name);
    }

    [Fact]
    public async Task GetExercisesAsync_NothingMatches_ReturnsEmptyList()
    {
        var result = await this.service.GetExercisesAsync("неіснуюча вправа", null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetExercisesAsync_LogsQueryAndResult()
    {
        await this.service.GetExercisesAsync("жим", EquipmentType.Gym);

        Assert.Equal(2, this.logger.Collector.Count);
    }

    [Fact]
    public async Task SaveExerciseAsync_Trainer_CreatesExercise()
    {
        var management = this.CreateManagementService();

        var result = await management.SaveExerciseAsync(NewRequest(), UserRole.Trainer);

        Assert.True(result.IsSuccess);
        this.managementRepository.Verify(
            r => r.AddAsync(
                It.Is<Exercise>(e => e.Name == "Жим гантелей" && e.EquipmentType == EquipmentType.Gym),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveExerciseAsync_RegularUser_FailsWithNotATrainer()
    {
        var management = this.CreateManagementService();

        var result = await management.SaveExerciseAsync(NewRequest(), UserRole.User);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotATrainer, result.ErrorCode);
        this.managementRepository.Verify(
            r => r.AddAsync(It.IsAny<Exercise>(), It.IsAny<CancellationToken>()),
            Times.Never);
        this.AssertWarningLogged();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    public async Task SaveExerciseAsync_InvalidName_FailsWithValidationError(string name)
    {
        var management = this.CreateManagementService();

        var result = await management.SaveExerciseAsync(NewRequest(name), UserRole.Trainer);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("Name", result.Field);
        this.AssertWarningLogged();
    }

    [Fact]
    public async Task SaveExerciseAsync_MissingTechnique_FailsWithValidationError()
    {
        var management = this.CreateManagementService();

        var result = await management.SaveExerciseAsync(NewRequest(technique: " "), UserRole.Admin);

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal("Technique", result.Field);
    }

    [Fact]
    public async Task SaveExerciseAsync_NameTakenIgnoringCase_FailsWithNameTaken()
    {
        var management = this.CreateManagementService(
            new Exercise { Id = 1, Name = "Жим гантелей", EquipmentType = EquipmentType.Gym });

        var result = await management.SaveExerciseAsync(NewRequest("жим ГАНТЕЛЕЙ"), UserRole.Trainer);

        Assert.Equal(ErrorCodes.ExerciseNameTaken, result.ErrorCode);
        this.managementRepository.Verify(
            r => r.AddAsync(It.IsAny<Exercise>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveExerciseAsync_ExistingExercise_UpdatesDescriptionAndTechnique()
    {
        var management = this.CreateManagementService();
        this.managementRepository
            .Setup(r => r.UpdateAsync(5, "Новий опис", "Нова техніка", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await management.SaveExerciseAsync(
            new SaveExerciseRequest(5, "Армійський жим", EquipmentType.Gym, "Новий опис", "Нова техніка"),
            UserRole.Trainer);

        Assert.True(result.IsSuccess);
        this.managementRepository.Verify(
            r => r.UpdateAsync(5, "Новий опис", "Нова техніка", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveExerciseAsync_UpdatingMissingExercise_FailsWithNotFound()
    {
        var management = this.CreateManagementService();
        this.managementRepository
            .Setup(r => r.UpdateAsync(99, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await management.SaveExerciseAsync(
            new SaveExerciseRequest(99, "Невідома вправа", EquipmentType.Gym, "Опис", "Техніка"),
            UserRole.Trainer);

        Assert.Equal(ErrorCodes.ExerciseNotFound, result.ErrorCode);
        this.AssertWarningLogged();
    }

    [Fact]
    public async Task ArchiveExerciseAsync_Admin_SoftDeletesExercise()
    {
        var management = this.CreateManagementService();
        this.managementRepository
            .Setup(r => r.SoftDeleteAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await management.ArchiveExerciseAsync(3, UserRole.Admin);

        Assert.True(result.IsSuccess);
        this.managementRepository.Verify(
            r => r.SoftDeleteAsync(3, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveExerciseAsync_NotFoundOrAlreadyDeleted_FailsWithNotFound()
    {
        var management = this.CreateManagementService();
        this.managementRepository
            .Setup(r => r.SoftDeleteAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await management.ArchiveExerciseAsync(42, UserRole.Trainer);

        Assert.Equal(ErrorCodes.ExerciseNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ArchiveExerciseAsync_RegularUser_FailsWithNotATrainer()
    {
        var management = this.CreateManagementService();

        var result = await management.ArchiveExerciseAsync(3, UserRole.User);

        Assert.Equal(ErrorCodes.NotATrainer, result.ErrorCode);
        this.managementRepository.Verify(
            r => r.SoftDeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static SaveExerciseRequest NewRequest(
        string name = "Жим гантелей",
        string? technique = "Опускайте гантелі підконтрольно.") =>
        new(null, name, EquipmentType.Gym, "Опис", technique);

    private ExerciseCatalogService CreateManagementService(params Exercise[] existing)
    {
        this.managementRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        this.managementRepository
            .Setup(r => r.AddAsync(It.IsAny<Exercise>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Exercise e, CancellationToken _) => e);
        return new ExerciseCatalogService(this.managementRepository.Object, this.managementLogger);
    }

    private void AssertWarningLogged() =>
        Assert.Contains(
            this.managementLogger.Collector.GetSnapshot(),
            record => record.Level == LogLevel.Warning);
}