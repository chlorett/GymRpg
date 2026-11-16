namespace MyApp.Application.Dtos;

public sealed record DeleteWorkoutRecordResultDto(
    int DeletedRecordId,
    int AdjustedXp,
    int NewLevel);
