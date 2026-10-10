using MyApp.Application.Dtos;
using MyApp.Application.Services;

namespace MyApp.Tests;

public class XpCalculatorTests
{
    [Fact]
    public void CalculateXp_WhenSetsNullOrEmpty_ReturnsZero()
    {
        // Act
        int xpNull = XpCalculator.CalculateXp(null);
        int xpEmpty = XpCalculator.CalculateXp(Array.Empty<WorkoutSetDto>());

        // Assert
        Assert.Equal(0, xpNull);
        Assert.Equal(0, xpEmpty);
    }

    [Fact]
    public void CalculateXp_WhenWeightedSetsProvided_CalculatesCorrectSum()
    {
        // Arrange
        var sets = new[]
        {
            new WorkoutSetDto(10, 50m), // 500
            new WorkoutSetDto(8, 60m),  // 480
        };

        // Act
        int result = XpCalculator.CalculateXp(sets);

        // Assert
        Assert.Equal(980, result);
    }

    [Fact]
    public void CalculateXp_WhenBodyweightSetsProvided_UsesDefaultWeight()
    {
        // Arrange
        var sets = new[]
        {
            new WorkoutSetDto(12, 0m), // 12 * 10 = 120
        };

        // Act
        int result = XpCalculator.CalculateXp(sets);

        // Assert
        Assert.Equal(120, result);
    }

    [Fact]
    public void CalculateXp_WhenNonPositiveReps_SkipsSet()
    {
        // Arrange
        var sets = new[]
        {
            new WorkoutSetDto(0, 50m),
            new WorkoutSetDto(-5, 50m),
            new WorkoutSetDto(5, 50m), // 250
        };

        // Act
        int result = XpCalculator.CalculateXp(sets);

        // Assert
        Assert.Equal(250, result);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-100, 1)]
    [InlineData(500, 1)]
    [InlineData(999, 1)]
    [InlineData(1000, 2)]
    [InlineData(1999, 2)]
    [InlineData(2000, 3)]
    [InlineData(10500, 11)]
    public void CalculateLevel_BasedOnTotalXp_ReturnsExpectedLevel(int totalXp, int expectedLevel)
    {
        // Act
        int level = XpCalculator.CalculateLevel(totalXp);

        // Assert
        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void IsSuspicious_WhenSetsNullOrEmpty_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(XpCalculator.IsSuspicious(null));
        Assert.False(XpCalculator.IsSuspicious(Array.Empty<WorkoutSetDto>()));
    }

    [Fact]
    public void IsSuspicious_WhenValuesWithinNormalRange_ReturnsFalse()
    {
        // Arrange
        var sets = new[]
        {
            new WorkoutSetDto(12, 80m),
            new WorkoutSetDto(10, 100m),
        };

        // Act
        bool result = XpCalculator.IsSuspicious(sets);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSuspicious_WhenWeightExceedsThreshold_ReturnsTrue()
    {
        // Arrange
        var sets = new[]
        {
            new WorkoutSetDto(5, 450m),
        };

        // Act
        bool result = XpCalculator.IsSuspicious(sets);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSuspicious_WhenRepsExceedThreshold_ReturnsTrue()
    {
        // Arrange
        var sets = new[]
        {
            new WorkoutSetDto(120, 20m),
        };

        // Act
        bool result = XpCalculator.IsSuspicious(sets);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSuspicious_WhenSetsCountExceedsThreshold_ReturnsTrue()
    {
        // Arrange
        var sets = Enumerable.Range(1, 35)
            .Select(_ => new WorkoutSetDto(5, 50m))
            .ToList();

        // Act
        bool result = XpCalculator.IsSuspicious(sets);

        // Assert
        Assert.True(result);
    }
}
