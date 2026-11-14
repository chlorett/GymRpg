namespace MyApp.Application.Dtos;

public sealed record DeleteWorkoutRecordResultDto(
    OperationResult Result,
    int DeletedRecordId,
    int AdjustedXp,
    int NewLevel);
