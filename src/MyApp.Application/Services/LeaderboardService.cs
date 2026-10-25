using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Application.Services;

public sealed class LeaderboardService : ILeaderboardService
{
    private readonly IUserProgressRepository _progressRepository;
    private readonly ILogger<LeaderboardService> _logger;

    public LeaderboardService(
        IUserProgressRepository progressRepository,
        ILogger<LeaderboardService> logger)
    {
        _progressRepository = progressRepository ?? throw new ArgumentNullException(nameof(progressRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static string GetRankTitle(int level) => level switch
    {
        <= 1 => "Новачок залу",
        <= 3 => "Залізний аматор",
        <= 5 => "Досвідчений атлет",
        <= 10 => "Ветеран штанги",
        _ => "Легенда PulseForge",
    };

    public async Task<LeaderboardDto> GetLeaderboardAsync(
        int currentUserId,
        string? searchQuery = null,
        CancellationToken ct = default)
    {
        if (currentUserId <= 0)
        {
            _logger.LogWarning("Leaderboard requested with non-positive currentUserId: {UserId}", currentUserId);
        }

        _logger.LogDebug(
            "Loading leaderboard for user {UserId} with search filter '{SearchQuery}'",
            currentUserId,
            searchQuery ?? string.Empty);

        var users = await _progressRepository.GetLeaderboardUsersAsync(ct);

        var rankedEntries = BuildRankedEntries(users, currentUserId);
        var currentEntry = rankedEntries.Find(e => e.IsCurrentUser);

        var filteredEntries = ApplySearchFilter(rankedEntries, searchQuery);

        _logger.LogInformation(
            "Leaderboard loaded: {TotalCount} active athletes, {FilteredCount} shown, current user {UserId} rank: {Rank}",
            rankedEntries.Count,
            filteredEntries.Count,
            currentUserId,
            currentEntry?.Rank);

        return new LeaderboardDto(
            currentEntry?.Rank,
            currentEntry?.Level ?? 1,
            currentEntry?.TotalXp ?? 0,
            rankedEntries.Count,
            filteredEntries);
    }

    private static List<LeaderboardEntryDto> BuildRankedEntries(
        IReadOnlyList<User> users,
        int currentUserId)
    {
        var eligible = users
            .Where(u => !u.IsBlocked && !u.IsExcludedFromLeaderboard)
            .Select(u =>
            {
                long totalXp = Math.Max(0, u.UserProgress?.Xp ?? 0);
                int level = u.UserProgress is not null && u.UserProgress.Level > 0
                    ? u.UserProgress.Level
                    : XpCalculator.CalculateLevel((int)Math.Min(totalXp, int.MaxValue));

                return (User: u, Level: level, TotalXp: totalXp);
            })
            .OrderByDescending(x => x.TotalXp)
            .ThenByDescending(x => x.Level)
            .ThenBy(x => x.User.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<LeaderboardEntryDto>(eligible.Count);
        for (int i = 0; i < eligible.Count; i++)
        {
            var item = eligible[i];
            int userId = (int)item.User.Id;
            result.Add(new LeaderboardEntryDto(
                Rank: i + 1,
                UserId: userId,
                Username: item.User.Username,
                Level: item.Level,
                TotalXp: item.TotalXp,
                RankTitle: GetRankTitle(item.Level),
                IsCurrentUser: userId == currentUserId));
        }

        return result;
    }

    private static IReadOnlyList<LeaderboardEntryDto> ApplySearchFilter(
        List<LeaderboardEntryDto> entries,
        string? searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return entries;
        }

        var normalized = searchQuery.Trim();
        return entries
            .Where(e => e.Username.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
