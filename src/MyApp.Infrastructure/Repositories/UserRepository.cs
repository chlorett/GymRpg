using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private static readonly string DefaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");

    private static readonly List<User> InMemUsers = new()
    {
        new User
        {
            Id = 1,
            Email = "athlete@gym.com",
            Username = "athlete",
            PasswordHash = DefaultPasswordHash,
            Role = UserRole.User,
            WeightKg = 75.0m,
            HeightCm = 180.0m,
            BirthDate = new DateTime(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        },
        new User
        {
            Id = 2,
            Email = "trainer@gym.com",
            Username = "coach_ivan",
            PasswordHash = DefaultPasswordHash,
            Role = UserRole.Trainer,
            WeightKg = 85.0m,
            HeightCm = 185.0m,
            BirthDate = new DateTime(1994, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        },
    };

    private readonly AppDbContext _db;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(AppDbContext db, ILogger<UserRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _logger.LogDebug("Loading user {UserId}", id);

        try
        {
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id, ct);

            if (user is not null)
            {
                return user;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка зв'язку з БД при пошуку користувача {UserId}, перевіряємо локальний список", id);
        }

        lock (InMemUsers)
        {
            return InMemUsers.Find(u => u.Id == id);
        }
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        _logger.LogDebug("Looking up user by email");

        var normalized = email.ToLowerInvariant();

        try
        {
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, ct);

            if (user is not null)
            {
                return user;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка зв'язку з БД при пошуку користувача за email, перевіряємо локальний список");
        }

        lock (InMemUsers)
        {
            return InMemUsers.Find(u => u.Email.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        }
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(username);

        _logger.LogDebug("Looking up user by username {Username}", username);

        var normalized = username.ToLowerInvariant();

        try
        {
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized, ct);

            if (user is not null)
            {
                return user;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка зв'язку з БД при пошуку користувача за username, перевіряємо локальний список");
        }

        lock (InMemUsers)
        {
            return InMemUsers.Find(u => u.Username.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        }
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        _logger.LogDebug("Saving user {Username}", user.Username);

        bool dbSaved = false;
        try
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
            dbSaved = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка збереження користувача в БД, зберігаємо в локальний список");
        }

        lock (InMemUsers)
        {
            if (!dbSaved && user.Id == 0)
            {
                user.Id = InMemUsers.Count > 0 ? InMemUsers.Max(u => u.Id) + 1 : 1;
            }

            if (!InMemUsers.Exists(u => u.Id == user.Id || u.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)))
            {
                InMemUsers.Add(user);
            }
        }
    }

    public async Task<bool> UpdateProfileAsync(
        int userId,
        decimal? weightKg,
        decimal? heightCm,
        DateOnly? birthDate,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Updating profile for user {UserId}", userId);

        try
        {
            // Без AsNoTracking, щоб EF відстежив зміни
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user is not null)
            {
                ApplyProfile(user, weightKg, heightCm, birthDate);
                await _db.SaveChangesAsync(ct);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка збереження профілю {UserId} в БД, оновлюємо локальний список", userId);
        }

        lock (InMemUsers)
        {
            var local = InMemUsers.Find(u => u.Id == userId);
            if (local is null)
            {
                return false;
            }

            ApplyProfile(local, weightKg, heightCm, birthDate);
            return true;
        }
    }

    public async Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);

        _logger.LogDebug("Updating password for user {UserId}", userId);

        try
        {
            // Без AsNoTracking, щоб EF відстежив зміни
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user is not null)
            {
                ApplyPassword(user, passwordHash);
                await _db.SaveChangesAsync(ct);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка оновлення пароля {UserId} в БД, оновлюємо локальний список", userId);
        }

        lock (InMemUsers)
        {
            var local = InMemUsers.Find(u => u.Id == userId);
            if (local is null)
            {
                return false;
            }

            ApplyPassword(local, passwordHash);
            return true;
        }
    }

    public async Task<IReadOnlyList<Region>> GetRegionsAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Loading regions");

        try
        {
            return await _db.Regions.AsNoTracking().OrderBy(r => r.Id).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка зв'язку з БД при завантаженні регіонів, використовуємо довідник за замовчуванням");
            return AffiliationSeedData.CreateFallbackRegions();
        }
    }

    public async Task<IReadOnlyList<Gym>> GetGymsAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Loading gyms");

        try
        {
            return await _db.Gyms.AsNoTracking().OrderBy(g => g.Id).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка зв'язку з БД при завантаженні спортзалів, використовуємо довідник за замовчуванням");
            return AffiliationSeedData.CreateFallbackGyms();
        }
    }

    public async Task<IReadOnlyList<Division>> GetDivisionsAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Loading divisions");

        try
        {
            return await _db.Divisions.AsNoTracking().OrderBy(d => d.Id).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка зв'язку з БД при завантаженні дивізіонів, використовуємо довідник за замовчуванням");
            return AffiliationSeedData.CreateDivisions();
        }
    }

    public async Task<int> CountByGymAsync(int gymId, CancellationToken ct = default)
    {
        _logger.LogDebug("Counting users of gym {GymId}", gymId);

        try
        {
            return await _db.Users.CountAsync(u => u.GymId == gymId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка зв'язку з БД при підрахунку атлетів залу {GymId}, перевіряємо локальний список", gymId);
        }

        lock (InMemUsers)
        {
            return InMemUsers.Count(u => u.GymId == gymId);
        }
    }

    public async Task<bool> UpdateAffiliationAsync(
        int userId,
        string username,
        int? gymId,
        int? divisionId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);

        _logger.LogDebug("Updating affiliation for user {UserId}", userId);

        User? tracked = null;
        try
        {
            // Без AsNoTracking, щоб EF відстежив зміни
            tracked = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (tracked is not null)
            {
                ApplyAffiliation(tracked, username, gymId, divisionId);
                await _db.SaveChangesAsync(ct);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка збереження приналежності {UserId} в БД, оновлюємо локальний список", userId);

            // Щоб невдала зміна не залишилась у відстежуваному контексті й не ламала наступні збереження.
            if (tracked is not null)
            {
                _db.Entry(tracked).State = EntityState.Detached;
            }
        }

        lock (InMemUsers)
        {
            var local = InMemUsers.Find(u => u.Id == userId);
            if (local is null)
            {
                return false;
            }

            ApplyAffiliation(local, username, gymId, divisionId);
            return true;
        }
    }

    public async Task<IReadOnlyList<GymRosterMemberDto>> GetGymRosterAsync(int? gymId, int? divisionId)
    {
        var query = _db.Users.AsQueryable();

        if (gymId.HasValue)
        {
            query = query.Where(u => u.GymId == gymId.Value);
        }

        if (divisionId.HasValue)
        {
            query = query.Where(u => u.DivisionId == divisionId.Value);
        }

        var users = await query.ToListAsync();
        var list = new List<GymRosterMemberDto>();

        foreach (var u in users)
        {
            list.Add(new GymRosterMemberDto
            {
                UserId = (int)u.Id,
                Nickname = u.Username ?? string.Empty,
                Role = u.Role.ToString(),
                GymId = u.GymId,
                GymName = u.GymId.HasValue ? "Зал #" + u.GymId.Value : "Не обрано",
                DivisionId = u.DivisionId,
                DivisionName = u.DivisionId.HasValue ? "Дивізіон #" + u.DivisionId.Value : "Не обрано",
                Level = 1,
                Xp = 0,
                SuspiciousWorkoutsCount = 0,
                IsExcludedFromLeaderboard = u.IsExcludedFromLeaderboard,
                IsBlocked = u.IsBlocked,
                BlockedAt = u.BlockedAt
            });
        }

        return list;
    }

    public async Task<bool> UpdateModerationStatusAsync(
        int userId,
        bool isExcludedFromLeaderboard,
        bool isBlocked,
        DateTimeOffset? blockedAt)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        user.IsExcludedFromLeaderboard = isExcludedFromLeaderboard;
        user.IsBlocked = isBlocked;
        user.BlockedAt = blockedAt;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    internal static IReadOnlyList<User> GetInMemoryUsersSnapshot()
    {
        lock (InMemUsers)
        {
            return InMemUsers.ToList();
        }
    }

    private static void ApplyProfile(User user, decimal? weightKg, decimal? heightCm, DateOnly? birthDate)
    {
        if (weightKg is not null)
        {
            user.WeightKg = weightKg;
        }

        if (heightCm is not null)
        {
            user.HeightCm = heightCm;
        }

        if (birthDate is { } date)
        {
            user.BirthDate = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ApplyPassword(User user, string passwordHash)
    {
        user.PasswordHash = passwordHash;
        user.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ApplyAffiliation(User user, string username, int? gymId, int? divisionId)
    {
        user.Username = username;
        user.GymId = gymId;
        user.DivisionId = divisionId;
        user.UpdatedAt = DateTimeOffset.UtcNow;
    }
}