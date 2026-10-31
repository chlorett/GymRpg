using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IMedalRepository
{
    Task<IReadOnlyList<Medal>> GetAllMedalsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<UserMedal>> GetUserMedalsAsync(int userId, CancellationToken ct = default);

    Task AddUserMedalsAsync(IReadOnlyList<UserMedal> userMedals, CancellationToken ct = default);
}
