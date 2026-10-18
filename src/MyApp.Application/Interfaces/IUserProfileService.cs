using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IUserProfileService
{
    Task<UserProfileDto?> GetAsync(int userId, CancellationToken ct = default);

    Task<OperationResult> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default);
}