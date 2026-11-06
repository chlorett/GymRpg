namespace MyApp.Application.Dtos;

public sealed record RecalculateMuscleLoadResultDto(
    OperationResult Result,
    int UpdatedGroupsCount,
    decimal TotalLoadValue,
    string TopMuscleGroupName);
