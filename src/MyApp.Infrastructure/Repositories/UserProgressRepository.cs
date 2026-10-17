using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class UserProgressRepository : IUserProgressRepository
{
    private static readonly Dictionary<long, UserProgress> InMemProgress = new();
    private readonly AppDbContext _db;
    private readonly ILogger<UserProgressRepository> _logger;

    public UserProgressRepository(AppDbContext db, ILogger<UserProgressRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UserProgress?> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Loading progress for user {UserId}", userId);

        try
        {
            var progress = await _db.Set<UserProgress>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId, ct);

            if (progress is not null)
            {
                return progress;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка завантаження прогресу користувача {UserId} з БД, перевіряємо локальний кеш", userId);
        }

        lock (InMemProgress)
        {
            return InMemProgress.TryGetValue(userId, out var inMem) ? inMem : null;
        }
    }

    public async Task SaveAsync(UserProgress progress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        _logger.LogDebug("Saving progress for user {UserId}", progress.UserId);

        try
        {
            var exists = await _db.Set<UserProgress>().AnyAsync(item => item.UserId == progress.UserId, ct);
            if (exists)
            {
                _db.Set<UserProgress>().Update(progress);
            }
            else
            {
                _db.Set<UserProgress>().Add(progress);
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка збереження прогресу в БД, зберігаємо в локальний кеш");
            lock (InMemProgress)
            {
                InMemProgress[progress.UserId] = progress;
            }
        }
    }
}
