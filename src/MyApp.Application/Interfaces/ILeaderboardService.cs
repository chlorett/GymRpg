using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface ILeaderboardService
{
    Task<LeaderboardDto> GetLeaderboardAsync(
        int currentUserId,
        string? searchQuery = null,
        CancellationToken ct = default);
}
