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
}
