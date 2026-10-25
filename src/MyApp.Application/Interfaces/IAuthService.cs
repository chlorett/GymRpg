using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IAuthService
{
    Task<OperationResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    Task<OperationResult> LoginAsync(LoginRequest request, CancellationToken ct = default);

    Task<OperationResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
}
