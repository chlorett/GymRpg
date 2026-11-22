using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Application.Services;

public sealed class DivisionQualificationService : IDivisionQualificationService
{
    private const string NoValue = "—";

    // Кваліфікаційні пороги XP за назвами дивізіонів (довідник у БД зберігає лише назву).
    private static readonly Dictionary<string, long> ThresholdsByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Новачок"] = 0,
        ["Любитель"] = 1000,
        ["Просунутий"] = 3000,
        ["Професіонал"] = 6000,
        ["Еліта"] = 10000,
    };

    // Запасні пороги за порядком, якщо в довіднику з'явився дивізіон з іншою назвою.
    private static readonly long[] ThresholdsByPosition = { 0, 1000, 3000, 6000, 10000 };

    private readonly IUserRepository _users;
    private readonly IUserProgressRepository _progress;
    private readonly ILogger<DivisionQualificationService> _logger;

    public DivisionQualificationService(
        IUserRepository users,
        IUserProgressRepository progress,
        ILogger<DivisionQualificationService> logger)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OperationResult<DivisionQualificationOverviewDto>> GetDivisionQualificationOverviewAsync(
        int userId,
        int? divisionIdFilter = null,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            return RejectOverview(
                userId,
                OperationResult.Fail<DivisionQualificationOverviewDto>(
                    ErrorCodes.ValidationFailed,
                    nameof(userId),
                    "Некоректний ідентифікатор користувача."));
        }

        if (divisionIdFilter is <= 0)
        {
            return RejectOverview(
                userId,
                OperationResult.Fail<DivisionQualificationOverviewDto>(
                    ErrorCodes.ValidationFailed,
                    nameof(divisionIdFilter),
                    "Некоректний ідентифікатор дивізіону."));
        }

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return RejectOverview(
                userId,
                OperationResult.Fail<DivisionQualificationOverviewDto>(
                    ErrorCodes.UserNotFound,
                    nameof(userId),
                    "Користувача не знайдено."));
        }

        var divisions = BuildLadder(await _users.GetDivisionsAsync(ct));
        if (divisions.Count == 0)
        {
            return RejectOverview(
                userId,
                OperationResult.Fail<DivisionQualificationOverviewDto>(
                    ErrorCodes.DivisionNotFound,
                    null,
                    "Довідник дивізіонів порожній."));
        }

        var progress = await _progress.GetByUserIdAsync(userId, ct);
        var (xp, level) = ReadProgress(progress);

        var current = divisions.Find(d => d.Division.Id == user.DivisionId);
        var next = FindNext(divisions, current);
        var selected = divisionIdFilter is { } filterId
            ? divisions.Find(d => d.Division.Id == filterId)
            : current ?? divisions[0];

        if (selected is null)
        {
            return RejectOverview(
                userId,
                OperationResult.Fail<DivisionQualificationOverviewDto>(
                    ErrorCodes.DivisionNotFound,
                    nameof(divisionIdFilter),
                    "Обраного дивізіону не існує."));
        }

        var isFair = IsFair(user);
        var requirements = divisions
            .Select(d => new DivisionRequirementDto(
                d.Division.Id,
                d.Division.Name,
                d.MinXp,
                XpCalculator.CalculateLevel(ToInt(d.MinXp)),
                current is not null && d.Division.Id == current.Division.Id,
                xp >= d.MinXp))
            .ToList();

        var participants = await _progress.GetLeaderboardUsersAsync(ct);
        var gymNames = (await _users.GetGymsAsync(ct)).ToDictionary(g => g.Id, g => g.Name);
        var standings = BuildStandings(participants, selected.Division.Id, userId, gymNames);

        var xpToNext = next is null ? 0 : Math.Max(0, next.MinXp - xp);
        var canPromote = next is not null && isFair && xp >= next.MinXp;

        _logger.LogInformation(
            "Division overview loaded for {UserId}: current {CurrentDivisionId}, selected {SelectedDivisionId}, {Count} athletes, can promote {CanPromote}",
            userId,
            current?.Division.Id,
            selected.Division.Id,
            standings.Count,
            canPromote);

        return OperationResult.Success(new DivisionQualificationOverviewDto(
            current?.Division.Id,
            current?.Division.Name ?? NoValue,
            xp,
            level,
            next?.Division.Id,
            next?.Division.Name,
            xpToNext,
            canPromote,
            selected.Division.Id,
            selected.Division.Name,
            requirements,
            standings));
    }

    public async Task<OperationResult<DivisionPromotionResultDto>> PromoteToDivisionAsync(
        PromoteDivisionRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
        {
            return RejectPromotion(
                0,
                OperationResult.Fail<DivisionPromotionResultDto>(
                    ErrorCodes.ValidationFailed,
                    nameof(request),
                    "Запит не може бути порожнім."));
        }

        if (request.UserId <= 0)
        {
            return RejectPromotion(
                request.UserId,
                OperationResult.Fail<DivisionPromotionResultDto>(
                    ErrorCodes.ValidationFailed,
                    nameof(PromoteDivisionRequest.UserId),
                    "Некоректний ідентифікатор користувача."));
        }

        if (request.TargetDivisionId <= 0)
        {
            return RejectPromotion(
                request.UserId,
                OperationResult.Fail<DivisionPromotionResultDto>(
                    ErrorCodes.ValidationFailed,
                    nameof(PromoteDivisionRequest.TargetDivisionId),
                    "Оберіть дивізіон для переходу."));
        }

        var user = await _users.GetByIdAsync(request.UserId, ct);
        if (user is null)
        {
            return RejectPromotion(
                request.UserId,
                OperationResult.Fail<DivisionPromotionResultDto>(
                    ErrorCodes.UserNotFound,
                    nameof(PromoteDivisionRequest.UserId),
                    "Користувача не знайдено."));
        }

        if (!IsFair(user))
        {
            return RejectPromotion(
                request.UserId,
                OperationResult.Fail<DivisionPromotionResultDto>(
                    ErrorCodes.ModerationAccessDenied,
                    nameof(PromoteDivisionRequest.UserId),
                    "Підвищення в дивізіоні недоступне: акаунт заблоковано або виключено з рейтингу за порушення Fair Play."));
        }

        var divisions = BuildLadder(await _users.GetDivisionsAsync(ct));
        var target = divisions.Find(d => d.Division.Id == request.TargetDivisionId);
        if (target is null)
        {
            return RejectPromotion(
                request.UserId,
                OperationResult.Fail<DivisionPromotionResultDto>(
                    ErrorCodes.DivisionNotFound,
                    nameof(PromoteDivisionRequest.TargetDivisionId),
                    "Обраного дивізіону не існує."));
        }

        var current = divisions.Find(d => d.Division.Id == user.DivisionId);
        if (current is not null && target.MinXp <= current.MinXp)
        {
            return RejectPromotion(
                request.UserId,
                OperationResult.Fail<DivisionPromotionResultDto>(
                    ErrorCodes.ValidationFailed,
                    nameof(PromoteDivisionRequest.TargetDivisionId),
                    "Перейти можна лише у вищий дивізіон."));
        }

        var progress = await _progress.GetByUserIdAsync(request.UserId, ct);
        var (xp, level) = ReadProgress(progress);

        if (xp < target.MinXp)
        {
            return RejectPromotion(
                request.UserId,
                OperationResult.Fail<DivisionPromotionResultDto>(
                    ErrorCodes.ValidationFailed,
                    nameof(PromoteDivisionRequest.TargetDivisionId),
                    $"Недостатньо XP для дивізіону «{target.Division.Name}»: потрібно ще {target.MinXp - xp} XP."));
        }

        var saved = await _users.UpdateAffiliationAsync(
            request.UserId,
            user.Username,
            user.GymId,
            target.Division.Id,
            ct);
        if (!saved)
        {
            return RejectPromotion(
                request.UserId,
                OperationResult.Fail<DivisionPromotionResultDto>(
                    ErrorCodes.UserNotFound,
                    nameof(PromoteDivisionRequest.UserId),
                    "Не вдалося зберегти зміну дивізіону."));
        }

        _logger.LogInformation(
            "User {UserId} promoted from division {PreviousDivisionId} to {NewDivisionId} with {Xp} XP",
            request.UserId,
            current?.Division.Id,
            target.Division.Id,
            xp);

        return OperationResult.Success(new DivisionPromotionResultDto(
            current?.Division.Id,
            current?.Division.Name ?? NoValue,
            target.Division.Id,
            target.Division.Name,
            xp,
            level));
    }

    private static bool IsFair(User user) => !user.IsBlocked && !user.IsExcludedFromLeaderboard;

    private static int ToInt(long value) => (int)Math.Min(value, int.MaxValue);

    private static (long Xp, int Level) ReadProgress(UserProgress? progress)
    {
        long xp = Math.Max(0, progress?.Xp ?? 0);
        int level = progress is not null && progress.Level > 0
            ? progress.Level
            : XpCalculator.CalculateLevel(ToInt(xp));

        return (xp, level);
    }

    private static List<LadderStep> BuildLadder(IReadOnlyList<Division> divisions)
    {
        return divisions
            .OrderBy(d => d.Id)
            .Select((d, index) => new LadderStep(d, ResolveThreshold(d.Name, index)))
            .OrderBy(step => step.MinXp)
            .ThenBy(step => step.Division.Id)
            .ToList();
    }

    private static long ResolveThreshold(string name, int position)
    {
        if (ThresholdsByName.TryGetValue(name.Trim(), out var byName))
        {
            return byName;
        }

        return ThresholdsByPosition[Math.Min(position, ThresholdsByPosition.Length - 1)];
    }

    private static LadderStep? FindNext(List<LadderStep> ladder, LadderStep? current)
    {
        return current is null
            ? ladder[0]
            : ladder.Find(step => step.MinXp > current.MinXp);
    }

    private static List<DivisionStandingEntryDto> BuildStandings(
        IReadOnlyList<User> users,
        int divisionId,
        int currentUserId,
        IReadOnlyDictionary<int, string> gymNames)
    {
        var ranked = users
            .Where(u => u.DivisionId == divisionId && IsFair(u))
            .Select(u =>
            {
                var (xp, level) = ReadProgress(u.UserProgress);
                return (User: u, Xp: xp, Level: level);
            })
            .OrderByDescending(x => x.Xp)
            .ThenByDescending(x => x.Level)
            .ThenBy(x => x.User.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<DivisionStandingEntryDto>(ranked.Count);
        for (int i = 0; i < ranked.Count; i++)
        {
            var item = ranked[i];
            int id = (int)item.User.Id;
            var gymName = item.User.GymId is { } gymId && gymNames.TryGetValue(gymId, out var name)
                ? name
                : NoValue;

            result.Add(new DivisionStandingEntryDto(
                i + 1,
                id,
                item.User.Username,
                gymName,
                item.Xp,
                item.Level,
                id == currentUserId));
        }

        return result;
    }

    private OperationResult<DivisionQualificationOverviewDto> RejectOverview(
        int userId,
        OperationResult<DivisionQualificationOverviewDto> result)
    {
        _logger.LogWarning(
            "Division overview rejected for {UserId}: error {ErrorCode}, field {Field}",
            userId,
            result.ErrorCode,
            result.Field);

        return result;
    }

    private OperationResult<DivisionPromotionResultDto> RejectPromotion(
        int userId,
        OperationResult<DivisionPromotionResultDto> result)
    {
        _logger.LogWarning(
            "Division promotion rejected for {UserId}: error {ErrorCode}, field {Field}",
            userId,
            result.ErrorCode,
            result.Field);

        return result;
    }

    private sealed record LadderStep(Division Division, long MinXp);
}
