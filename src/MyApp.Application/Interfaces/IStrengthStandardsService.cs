using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IStrengthStandardsService
{
    Task<OperationResult<StrengthStandardsOverviewDto>> GetStrengthStandardsOverviewAsync(
        int userId,
        string? rankFilter = null,
        CancellationToken ct = default);

    Task<OperationResult<StrengthTargetPlanDto>> CalculateNextStandardTargetAsync(
        int userId,
        int exerciseId,
        int targetReps = 5,
        CancellationToken ct = default);
}
