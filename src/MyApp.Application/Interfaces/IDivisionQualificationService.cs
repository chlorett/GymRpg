using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IDivisionQualificationService
{
    Task<OperationResult<DivisionQualificationOverviewDto>> GetDivisionQualificationOverviewAsync(
        int userId,
        int? divisionIdFilter = null,
        CancellationToken ct = default);

    Task<OperationResult<DivisionPromotionResultDto>> PromoteToDivisionAsync(
        PromoteDivisionRequest request,
        CancellationToken ct = default);
}
