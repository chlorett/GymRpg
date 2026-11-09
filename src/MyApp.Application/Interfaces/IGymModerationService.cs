using System.Threading.Tasks;
using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IGymModerationService
{
    Task<GymRosterOverviewDto> GetGymRosterAsync(int? gymId, int? divisionId, string? searchQuery = null);

    Task<bool> UpdateModerationStatusAsync(UpdateAthleteModerationRequest request);
}