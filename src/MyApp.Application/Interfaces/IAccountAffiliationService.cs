using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IAccountAffiliationService
{
    // Повертає null, якщо користувача не існує.
    Task<UserAffiliationDto?> GetAffiliationDataAsync(int userId, CancellationToken ct = default);

    // GymId і DivisionId описують бажаний стан: null означає «без залу» / «без дивізіону».
    Task<OperationResult> UpdateAffiliationAsync(UpdateAffiliationRequest request, CancellationToken ct = default);
}
