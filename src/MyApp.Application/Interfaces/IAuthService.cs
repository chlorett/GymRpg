using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IAuthService
{
    Task<OperationResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
}
