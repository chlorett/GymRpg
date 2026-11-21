namespace MyApp.Application.Dtos;

public sealed record RecoveryOverviewDto(
    int PeriodDays,
    int CurrentStreakDays,
    int BestStreakDays,
    int WorkoutsInPeriod,
    int TotalSetsInPeriod,
    decimal TotalVolumeInPeriodKg,
    int EarnedXpInPeriod,
    int ReadinessScorePercent,
    string ReadinessSummaryText,
    IReadOnlyList<MuscleRecoveryStatusItemDto> MuscleRecoveryItems);
