using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public sealed class GymStandingsService : IGymStandingsService
{
    private const int MinGymNameLength = 3;
    private const int MaxGymNameLength = 150;
    private const string NoLeader = "—";

    private readonly IUserRepository _users;
    private readonly ILogger<GymStandingsService> _logger;

    public GymStandingsService(IUserRepository users, ILogger<GymStandingsService> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<GymStandingsOverviewDto> GetStandingsAsync(
        int userId,
        int? regionId,
        int? divisionId,
        CancellationToken ct = default)
    {
        var stats = await _users.GetGymClubStatsAsync(regionId, divisionId, ct);

        // При фільтрі за дивізіоном залишаємо лише зали, де є атлети цього дивізіону.
        var visible = divisionId is null
            ? stats
            : stats.Where(s => s.ActiveAthletes > 0).ToList();

        var standings = visible
            .OrderByDescending(s => s.TotalXp)
            .ThenByDescending(s => s.ActiveAthletes)
            .ThenBy(s => s.GymName, StringComparer.CurrentCultureIgnoreCase)
            .Select((s, index) => new GymStandingDto(
                index + 1,
                s.GymId,
                s.GymName,
                s.RegionName,
                s.ActiveAthletes,
                s.TotalXp,
                CalculateAverage(s.TotalXp, s.ActiveAthletes),
                string.IsNullOrWhiteSpace(s.LeaderNickname) ? NoLeader : s.LeaderNickname))
            .ToList();

        var regions = (await _users.GetRegionsAsync(ct))
            .Select(r => new RegionDto(r.Id, r.Name))
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var divisions = (await _users.GetDivisionsAsync(ct))
            .Select(d => new DivisionDto(d.Id, d.Name))
            .ToList();

        var user = userId > 0 ? await _users.GetByIdAsync(userId, ct) : null;
        var canRegister = user is not null && user.Role != UserRole.User;

        _logger.LogDebug(
            "Gym standings loaded for {UserId}: {GymCount} gyms, region {RegionId}, division {DivisionId}",
            userId,
            standings.Count,
            regionId,
            divisionId);

        return new GymStandingsOverviewDto(standings, regions, divisions, canRegister);
    }

    public async Task<OperationResult> RegisterGymAsync(RegisterGymRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            _logger.LogWarning(
                "Gym registration rejected: error {ErrorCode}, field {Field}",
                ErrorCodes.ValidationFailed,
                nameof(request));
            return OperationResult.Fail(ErrorCodes.ValidationFailed, nameof(request), "Запит не може бути порожнім.");
        }

        var user = request.UserId > 0 ? await _users.GetByIdAsync(request.UserId, ct) : null;
        if (user is null || user.Role == UserRole.User)
        {
            var denied = OperationResult.Fail(
                ErrorCodes.ModerationAccessDenied,
                nameof(RegisterGymRequest.UserId),
                "Реєструвати спортзали можуть лише тренери та адміністратори.");
            return Reject(denied, request.UserId);
        }

        var name = (request.Name ?? string.Empty).Trim();

        var nameError = GetNameError(name);
        if (nameError is not null)
        {
            var invalidName = OperationResult.Fail(ErrorCodes.ValidationFailed, nameof(RegisterGymRequest.Name), nameError);
            return Reject(invalidName, request.UserId);
        }

        if (request.RegionId <= 0)
        {
            var noRegion = OperationResult.Fail(ErrorCodes.ValidationFailed, nameof(RegisterGymRequest.RegionId), "Оберіть регіон.");
            return Reject(noRegion, request.UserId);
        }

        var regions = await _users.GetRegionsAsync(ct);
        if (!regions.Any(r => r.Id == request.RegionId))
        {
            var missingRegion = OperationResult.Fail(ErrorCodes.RegionNotFound, nameof(RegisterGymRequest.RegionId), "Обраного регіону не існує.");
            return Reject(missingRegion, request.UserId);
        }

        var gyms = await _users.GetGymsAsync(ct);
        var duplicate = gyms.Any(g =>
            g.RegionId == request.RegionId &&
            string.Equals(g.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase));
        if (duplicate)
        {
            var taken = OperationResult.Fail(ErrorCodes.GymNameTaken, nameof(RegisterGymRequest.Name), "Спортзал з такою назвою вже є в цьому регіоні.");
            return Reject(taken, request.UserId);
        }

        var created = await _users.AddGymAsync(request.RegionId, name, ct);

        _logger.LogInformation(
            "Gym registered by {UserId}: {GymId} in region {RegionId}",
            request.UserId,
            created.Id,
            request.RegionId);

        return OperationResult.Success();
    }

    private static long CalculateAverage(long totalXp, int athletes)
    {
        return athletes <= 0
            ? 0
            : (long)Math.Round((double)totalXp / athletes, MidpointRounding.AwayFromZero);
    }

    private static string? GetNameError(string name)
    {
        if (name.Length == 0)
        {
            return "Вкажіть назву спортзалу.";
        }

        if (name.Length < MinGymNameLength)
        {
            return $"Назва має містити щонайменше {MinGymNameLength} символи.";
        }

        return name.Length > MaxGymNameLength
            ? $"Назва не може бути довшою за {MaxGymNameLength} символів."
            : null;
    }

    private OperationResult Reject(OperationResult result, int userId)
    {
        _logger.LogWarning(
            "Gym registration rejected for {UserId}: error {ErrorCode}, field {Field}",
            userId,
            result.ErrorCode,
            result.Field);

        return result;
    }
}
