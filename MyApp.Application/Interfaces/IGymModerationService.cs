using System.Threading.Tasks;
using MyApp.Application.Common;
using MyApp.Application.DTOs.Moderation;

namespace MyApp.Application.Interfaces
{
    public interface IGymModerationService
    {
        Task<OperationResult<GymRosterOverviewDto>> GetGymRosterAsync(int? gymId, int? divisionId, string? searchQuery);
        Task<OperationResult<bool>> UpdateModerationStatusAsync(UpdateAthleteModerationRequest request);
    }
}
