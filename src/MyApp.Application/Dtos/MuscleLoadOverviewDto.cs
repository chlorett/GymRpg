namespace MyApp.Application.Dtos;

public sealed record MuscleLoadOverviewDto(
    int TotalGroupsCount,
    int ActiveGroupsCount,
    decimal TotalLoadValue,
    string TopMuscleGroupName,
    string LeastLoadedMuscleGroupName,
    IReadOnlyList<MuscleGroupLoadItemDto> MuscleGroups);
