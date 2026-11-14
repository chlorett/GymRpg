namespace MyApp.Application.Dtos;

public sealed record PersonalRecordsOverviewDto(
    int TotalWorkoutsLogged,
    int ExercisesWithRecordsCount,
    decimal BestOneRepMaxKg,
    string BestExerciseName,
    IReadOnlyList<ExercisePersonalRecordDto> PersonalRecords,
    IReadOnlyList<WorkoutLogEntryDto> WorkoutLogs);
