using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IUserProgressRepository
{
    Task<UserProgress?> GetByUserIdAsync(int userId, CancellationToken ct = default);

    // Вставка, якщо запису ще немає (Id == 0), інакше оновлення.
    Task SaveAsync(UserProgress progress, CancellationToken ct = default);
}
