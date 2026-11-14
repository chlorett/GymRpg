using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IGymStandingsService
{
    Task<GymStandingsOverviewDto> GetStandingsAsync(
        int userId,
        int? regionId,
        int? divisionId,
        CancellationToken ct = default);

    Task<OperationResult> RegisterGymAsync(RegisterGymRequest request, CancellationToken ct = default);
}
