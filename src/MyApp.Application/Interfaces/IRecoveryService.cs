using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IRecoveryService
{
    Task<OperationResult<RecoveryOverviewDto>> GetRecoveryOverviewAsync(
        int userId,
        int periodDays = 7,
        CancellationToken ct = default);

    Task<OperationResult<DeloadResultDto>> ApplyRecoveryDeloadAsync(
        int userId,
        int reductionPercent = 30,
        CancellationToken ct = default);
}
