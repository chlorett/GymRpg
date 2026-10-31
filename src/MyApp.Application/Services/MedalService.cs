using System.Globalization;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public sealed class MedalService : IMedalService
{
    private const int MaxWorkoutHistoryLookup = 500;

    private readonly IMedalRepository _medalRepository;
    private readonly IUserProgressRepository _progressRepository;
    private readonly IWorkoutRecordRepository _workoutRecordRepository;
    private readonly IUserRepository _userRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MedalService> _logger;

    public MedalService(
        IMedalRepository medalRepository,
        IUserProgressRepository progressRepository,
        IWorkoutRecordRepository workoutRecordRepository,
        IUserRepository userRepository,
        TimeProvider timeProvider,
        ILogger<MedalService> logger)
    {
        _medalRepository = medalRepository ?? throw new ArgumentNullException(nameof(medalRepository));
        _progressRepository = progressRepository ?? throw new ArgumentNullException(nameof(progressRepository));
        _workoutRecordRepository = workoutRecordRepository ?? throw new ArgumentNullException(nameof(workoutRecordRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static string GetConditionCategoryText(MedalConditionType conditionType) => conditionType switch
    {
        MedalConditionType.WorkoutsCount => "Тренування",
        MedalConditionType.Level => "Рівень",
        MedalConditionType.TotalXp => "Досвід (XP)",
        MedalConditionType.StreakDays => "Дні активності",
        _ => "Досягнення",
    };

    public async Task<UserMedalsOverviewDto> GetUserMedalsOverviewAsync(
        int userId,
        string? statusFilter = null,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            _logger.LogWarning(
                "Medals overview requested with invalid userId: {UserId} [{ErrorCode}]",
                userId,
                ErrorCodes.ValidationFailed);
            return new UserMedalsOverviewDto(0, 0, 0, 0, Array.Empty<MedalItemDto>());
        }

        _logger.LogDebug(
            "Loading medals overview for user {UserId} with statusFilter '{StatusFilter}'",
            userId,
            statusFilter ?? string.Empty);

        var allMedals = await _medalRepository.GetAllMedalsAsync(ct);
        var userMedals = await _medalRepository.GetUserMedalsAsync(userId, ct);
        var metrics = await LoadAthleteMetricsAsync(userId, ct);

        var items = BuildMedalItems(allMedals, userMedals, metrics);
        int earnedCount = items.Count(m => m.IsEarned);
        int availableToClaimCount = items.Count(m => m.CanClaim);
        int completionPercent = items.Count == 0
            ? 0
            : (int)Math.Round(earnedCount * 100.0 / items.Count);

        var filteredItems = ApplyStatusFilter(items, statusFilter);

        _logger.LogInformation(
            "Medals overview loaded for user {UserId}: {EarnedCount}/{TotalCount} earned, {ClaimableCount} claimable, {ShownCount} shown",
            userId,
            earnedCount,
            items.Count,
            availableToClaimCount,
            filteredItems.Count);

        return new UserMedalsOverviewDto(
            TotalMedalsCount: items.Count,
            EarnedMedalsCount: earnedCount,
            AvailableToClaimCount: availableToClaimCount,
            CompletionPercent: completionPercent,
            Medals: filteredItems);
    }

    public async Task<ClaimMedalsResultDto> ClaimEligibleMedalsAsync(
        int userId,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            return RejectClaim(
                ErrorCodes.ValidationFailed,
                nameof(userId),
                "Некоректний ідентифікатор користувача.",
                userId);
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return RejectClaim(
                ErrorCodes.UserNotFound,
                nameof(userId),
                "Користувача не знайдено.",
                userId);
        }

        var allMedals = await _medalRepository.GetAllMedalsAsync(ct);
        var existingUserMedals = await _medalRepository.GetUserMedalsAsync(userId, ct);
        var earnedMedalIds = existingUserMedals.Select(um => um.MedalId).ToHashSet();

        var metrics = await LoadAthleteMetricsAsync(userId, ct);
        var now = _timeProvider.GetUtcNow();

        var newlyEarned = new List<UserMedal>();
        var claimedNames = new List<string>();

        foreach (var medal in allMedals.Where(m => !earnedMedalIds.Contains(m.Id)))
        {
            int currentValue = GetMetricValue(medal.ConditionType, metrics);
            if (currentValue >= medal.ConditionValue)
            {
                newlyEarned.Add(new UserMedal
                {
                    UserId = userId,
                    MedalId = medal.Id,
                    EarnedAt = now,
                });
                claimedNames.Add(medal.Name);
            }
        }

        if (newlyEarned.Count > 0)
        {
            await _medalRepository.AddUserMedalsAsync(newlyEarned, ct);
            _logger.LogInformation(
                "User {UserId} claimed {ClaimedCount} new medal(s): {MedalNames}",
                userId,
                newlyEarned.Count,
                string.Join(", ", claimedNames));
        }
        else
        {
            _logger.LogDebug("No new eligible medals to claim for user {UserId}", userId);
        }

        return new ClaimMedalsResultDto(
            OperationResult.Success(),
            newlyEarned.Count,
            claimedNames);
    }

    private static List<MedalItemDto> BuildMedalItems(
        IReadOnlyList<Medal> medals,
        IReadOnlyList<UserMedal> userMedals,
        (int TotalXp, int Level, int WorkoutsCount, int ActiveDaysCount) metrics)
    {
        var earnedByMedalId = userMedals
            .GroupBy(um => um.MedalId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.EarnedAt).First());

        var orderedMedals = medals
            .OrderBy(m => m.ConditionType)
            .ThenBy(m => m.ConditionValue)
            .ThenBy(m => m.Id)
            .ToList();

        var result = new List<MedalItemDto>(orderedMedals.Count);
        foreach (var medal in orderedMedals)
        {
            bool isEarned = earnedByMedalId.TryGetValue(medal.Id, out var userMedal);
            int targetValue = Math.Max(1, medal.ConditionValue);
            int currentValue = GetMetricValue(medal.ConditionType, metrics);
            bool canClaim = !isEarned && currentValue >= targetValue;

            int progressPercent = isEarned
                ? 100
                : (int)Math.Clamp(currentValue * 100L / targetValue, 0, 100);

            DateTimeOffset? earnedAt = userMedal?.EarnedAt;
            string statusText = BuildStatusText(isEarned, canClaim, earnedAt);
            int displayedCurrent = Math.Min(currentValue, targetValue);
            string progressText = $"{displayedCurrent} / {targetValue} ({progressPercent}%)";

            result.Add(new MedalItemDto(
                MedalId: medal.Id,
                Name: medal.Name,
                Description: medal.Description,
                ConditionType: medal.ConditionType,
                ConditionCategoryText: GetConditionCategoryText(medal.ConditionType),
                TargetValue: targetValue,
                CurrentValue: currentValue,
                ProgressPercent: progressPercent,
                IsEarned: isEarned,
                CanClaim: canClaim,
                EarnedAt: earnedAt,
                StatusText: statusText,
                ProgressText: progressText));
        }

        return result;
    }

    private static string BuildStatusText(bool isEarned, bool canClaim, DateTimeOffset? earnedAt)
    {
        if (isEarned)
        {
            return earnedAt is { } date
                ? $"Здобуто ({date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)})"
                : "Здобуто";
        }

        return canClaim ? "Готово до отримання!" : "Заблоковано";
    }

    private static int GetMetricValue(
        MedalConditionType conditionType,
        (int TotalXp, int Level, int WorkoutsCount, int ActiveDaysCount) metrics) => conditionType switch
    {
        MedalConditionType.TotalXp => metrics.TotalXp,
        MedalConditionType.Level => metrics.Level,
        MedalConditionType.WorkoutsCount => metrics.WorkoutsCount,
        MedalConditionType.StreakDays => metrics.ActiveDaysCount,
        _ => 0,
    };

    private static IReadOnlyList<MedalItemDto> ApplyStatusFilter(
        List<MedalItemDto> items,
        string? statusFilter)
    {
        if (string.IsNullOrWhiteSpace(statusFilter))
        {
            return items;
        }

        string normalized = statusFilter.Trim();
        if (normalized.Equals("earned", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Здобуті", StringComparison.OrdinalIgnoreCase))
        {
            return items.Where(m => m.IsEarned).ToList();
        }

        if (normalized.Equals("locked", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Заблоковані", StringComparison.OrdinalIgnoreCase))
        {
            return items.Where(m => !m.IsEarned).ToList();
        }

        return items;
    }

    private async Task<(int TotalXp, int Level, int WorkoutsCount, int ActiveDaysCount)> LoadAthleteMetricsAsync(
        int userId,
        CancellationToken ct)
    {
        var progress = await _progressRepository.GetByUserIdAsync(userId, ct);
        int totalXp = (int)Math.Clamp(progress?.Xp ?? 0, 0, int.MaxValue);
        int level = progress is not null && progress.Level > 0
            ? progress.Level
            : XpCalculator.CalculateLevel(totalXp);

        var records = await _workoutRecordRepository.GetRecentByUserIdAsync(
            userId,
            MaxWorkoutHistoryLookup,
            ct);

        var validRecords = records.Where(r => !r.IsSuspicious).ToList();
        int workoutsCount = validRecords.Count;
        int activeDaysCount = validRecords
            .Select(r => r.CreatedAt.UtcDateTime.Date)
            .Distinct()
            .Count();

        return (totalXp, level, workoutsCount, activeDaysCount);
    }

    private ClaimMedalsResultDto RejectClaim(
        string errorCode,
        string field,
        string message,
        int userId)
    {
        _logger.LogWarning(
            "Medal claim rejected for user {UserId}: [{ErrorCode}] on {Field} — {Message}",
            userId,
            errorCode,
            field,
            message);

        var failure = OperationResult.Fail(errorCode, field, message);
        return new ClaimMedalsResultDto(failure, 0, Array.Empty<string>());
    }
}
