using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class UserProgressRepository : IUserProgressRepository
{
    private static readonly Dictionary<long, UserProgress> InMemProgress = new()
    {
        [101] = new UserProgress { UserId = 101, Level = 3, Xp = 2450, UpdatedAt = DateTimeOffset.UtcNow },
        [102] = new UserProgress { UserId = 102, Level = 2, Xp = 1650, UpdatedAt = DateTimeOffset.UtcNow },
        [103] = new UserProgress { UserId = 103, Level = 2, Xp = 1120, UpdatedAt = DateTimeOffset.UtcNow },
        [104] = new UserProgress { UserId = 104, Level = 1, Xp = 480, UpdatedAt = DateTimeOffset.UtcNow },
    };

    private static readonly IReadOnlyList<(long Id, string Username, string Email, UserRole Role)> FallbackAthletes =
        new List<(long, string, string, UserRole)>
        {
            (1, "athlete", "athlete@gym.com", UserRole.User),
            (2, "coach_ivan", "trainer@gym.com", UserRole.Trainer),
            (101, "iron_max", "max@gym.com", UserRole.User),
            (102, "power_oleh", "oleh@gym.com", UserRole.User),
            (103, "fit_maria", "maria@gym.com", UserRole.User),
            (104, "rookie_artem", "artem@gym.com", UserRole.User),
        };

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

    public async Task<IReadOnlyList<User>> GetLeaderboardUsersAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Loading leaderboard users with progress");

        try
        {
            var dbUsers = await _db.Users
                .AsNoTracking()
                .Include(u => u.UserProgress)
                .ToListAsync(ct);

            if (dbUsers.Count > 0)
            {
                return dbUsers;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка завантаження лідерборду з БД, використовуємо резервні дані");
        }

        return BuildFallbackLeaderboardUsers();
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
        }

        lock (InMemProgress)
        {
            InMemProgress[progress.UserId] = progress;
        }
    }

    private static List<User> BuildFallbackLeaderboardUsers()
    {
        var registeredUsers = UserRepository.GetInMemoryUsersSnapshot();

        lock (InMemProgress)
        {
            var result = new List<User>(FallbackAthletes.Count + registeredUsers.Count);
            foreach (var item in FallbackAthletes)
            {
                InMemProgress.TryGetValue(item.Id, out var progress);
                result.Add(new User
                {
                    Id = item.Id,
                    Username = item.Username,
                    Email = item.Email,
                    Role = item.Role,
                    UserProgress = progress ?? CreateDefaultProgress(item.Id),
                });
            }

            foreach (var regUser in registeredUsers.Where(regUser => result.TrueForAll(u => u.Id != regUser.Id)))
            {
                InMemProgress.TryGetValue(regUser.Id, out var progress);
                result.Add(new User
                {
                    Id = regUser.Id,
                    Username = regUser.Username,
                    Email = regUser.Email,
                    Role = regUser.Role,
                    IsBlocked = regUser.IsBlocked,
                    IsExcludedFromLeaderboard = regUser.IsExcludedFromLeaderboard,
                    UserProgress = progress ?? CreateDefaultProgress(regUser.Id),
                });
            }

            foreach (var kvp in InMemProgress.Where(kvp => result.TrueForAll(u => u.Id != kvp.Key)))
            {
                result.Add(new User
                {
                    Id = kvp.Key,
                    Username = $"athlete_{kvp.Key}",
                    Email = $"user{kvp.Key}@gym.com",
                    Role = UserRole.User,
                    UserProgress = kvp.Value,
                });
            }

            return result;
        }
    }

    private static UserProgress CreateDefaultProgress(long userId) =>
        new()
        {
            UserId = userId,
            Level = 1,
            Xp = 0,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
}
