using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IMedalService
{
    Task<UserMedalsOverviewDto> GetUserMedalsOverviewAsync(
        int userId,
        string? statusFilter = null,
        CancellationToken ct = default);

    Task<ClaimMedalsResultDto> ClaimEligibleMedalsAsync(
        int userId,
        CancellationToken ct = default);
}
