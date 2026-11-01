using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);

    // Пошук без урахування регістру. Сервіс передає email уже в lowercase.
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    // Пошук без урахування регістру.
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);

    // null означає «залишити без змін». Повертає false, якщо користувача немає. Софія
    Task<bool> UpdateProfileAsync(
        int userId,
        decimal? weightKg,
        decimal? heightCm,
        DateOnly? birthDate,
        CancellationToken ct = default);

    // Оновлює лише хеш пароля й UpdatedAt. Повертає false, якщо користувача немає.
    Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken ct = default);

    Task<IReadOnlyList<Region>> GetRegionsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Gym>> GetGymsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Division>> GetDivisionsAsync(CancellationToken ct = default);

    // Кількість користувачів, закріплених за залом.
    Task<int> CountByGymAsync(int gymId, CancellationToken ct = default);

    // null у gymId / divisionId означає «без залу» / «без дивізіону». Повертає false, якщо користувача немає.
    Task<bool> UpdateAffiliationAsync(
        int userId,
        string username,
        int? gymId,
        int? divisionId,
        CancellationToken ct = default);
}
