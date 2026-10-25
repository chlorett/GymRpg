using Microsoft.Extensions.Logging.Testing;
using Moq;
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

        Assert.Equal(4, result.Count);
    }

    [Theory]
    [InlineData("ЖИМ ЛЕЖАЧИ")]
    [InlineData("жим лежачи")]
    [InlineData("  Жим лежачи  ")]
    public async Task GetExercisesAsync_SearchByName_IgnoresCase(string query)
    {
        var result = await this.service.GetExercisesAsync(query, null);

        var item = Assert.Single(result);
        Assert.Equal("Жим лежачи на лаві", item.Name);
    }

    [Fact]
    public async Task GetExercisesAsync_FilterByEquipmentType_ReturnsOnlyMatching()
    {
        var result = await this.service.GetExercisesAsync(null, EquipmentType.Calisthenics);

        Assert.Equal(2, result.Count);
        Assert.All(result, i => Assert.Equal(EquipmentType.Calisthenics, i.EquipmentType));
        Assert.All(result, i => Assert.Equal("Калістеніка / Власна вага", i.EquipmentLabel));
    }

    [Fact]
    public async Task GetExercisesAsync_SearchAndEquipmentFilter_AreCombined()
    {
        var result = await this.service.GetExercisesAsync("трицепс", EquipmentType.Calisthenics);

        var item = Assert.Single(result);
        Assert.Equal("Віджимання на брусах", item.Name);
    }

    [Fact]
    public async Task GetExercisesAsync_NothingMatches_ReturnsEmptyList()
    {
        var result = await this.service.GetExercisesAsync("неіснуюча вправа", null);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetExercisesAsync_LogsQueryAndResult()
    {
        await this.service.GetExercisesAsync("жим", EquipmentType.Gym);

        Assert.Equal(2, this.logger.Collector.Count);
    }
}
