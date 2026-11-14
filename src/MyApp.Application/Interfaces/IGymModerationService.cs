using System.Threading;
using System.Threading.Tasks;
using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IGymModerationService
{
    Task<GymRosterOverviewDto> GetGymRosterAsync(
        int? gymId,
        int? divisionId,
        string? searchQuery = null,
        CancellationToken ct = default);

    Task<OperationResult> UpdateModerationStatusAsync(
        UpdateAthleteModerationRequest request,
        CancellationToken ct = default);
}
