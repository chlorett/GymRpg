using MyApp.Domain.Enums;

namespace MyApp.Application.Dtos;

public sealed record MedalItemDto(
    int MedalId,
    string Name,
    string Description,
    MedalConditionType ConditionType,
    string ConditionCategoryText,
    int TargetValue,
    int CurrentValue,
    int ProgressPercent,
    bool IsEarned,
    bool CanClaim,
    DateTimeOffset? EarnedAt,
    string StatusText,
    string ProgressText);
