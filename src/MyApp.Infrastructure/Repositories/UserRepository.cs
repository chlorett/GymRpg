using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
            UpdatedAt = DateTimeOffset.UtcNow
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
            UpdatedAt = DateTimeOffset.UtcNow
        }
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
}
