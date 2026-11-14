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

    // Зали, додані в резервному режимі (коли PostgreSQL недоступний).
    private static readonly List<Gym> InMemGyms = new();

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
                if (string.Equals(user.Username, "sazerwar", StringComparison.OrdinalIgnoreCase))
                {
                    user.Role = UserRole.Admin;
                }

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
                if (string.Equals(user.Username, "sazerwar", StringComparison.OrdinalIgnoreCase))
                {
                    user.Role = UserRole.Admin;
                }

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
                if (string.Equals(user.Username, "sazerwar", StringComparison.OrdinalIgnoreCase))
                {
                    user.Role = UserRole.Admin;
                }

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
            return GetFallbackGyms();
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

    public async Task<IReadOnlyList<GymClubStatsDto>> GetGymClubStatsAsync(
        int? regionId,
        int? divisionId,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Loading gym club stats: region {RegionId}, division {DivisionId}", regionId, divisionId);

        try
        {
            var gymsQuery = _db.Gyms.AsNoTracking().AsQueryable();
            if (regionId is { } rid)
            {
                gymsQuery = gymsQuery.Where(g => g.RegionId == rid);
            }

            var gyms = await gymsQuery
                .Select(g => new GymRow(g.Id, g.Name, g.RegionId, g.Region.Name))
                .ToListAsync(ct);

            // Лише чесні атлети: не заблоковані й не виключені з лідерборду.
            var athletesQuery = _db.Users
                .AsNoTracking()
                .Where(u => u.GymId != null && !u.IsBlocked && !u.IsExcludedFromLeaderboard);
            if (divisionId is { } did)
            {
                athletesQuery = athletesQuery.Where(u => u.DivisionId == did);
            }

            var athletes = await athletesQuery
                .Select(u => new AthleteRow(
                    u.GymId!.Value,
                    u.Username,
                    u.UserProgress == null ? 0L : u.UserProgress.Xp))
                .ToListAsync(ct);

            return BuildClubStats(gyms, athletes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка зв'язку з БД при завантаженні рейтингу спортзалів, рахуємо з локальних даних");
        }

        return BuildFallbackClubStats(regionId, divisionId);
    }

    public async Task<Gym> AddGymAsync(int regionId, string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _logger.LogDebug("Saving gym {GymName} in region {RegionId}", name, regionId);

        var gym = new Gym { RegionId = regionId, Name = name };

        try
        {
            _db.Gyms.Add(gym);
            await _db.SaveChangesAsync(ct);
            return gym;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка збереження спортзалу в БД, зберігаємо в локальний список");

            // Невдала сутність не повинна залишитись у відстежуваному контексті.
            _db.Entry(gym).State = EntityState.Detached;
        }

        lock (InMemGyms)
        {
            var maxId = GetFallbackGyms().Max(g => g.Id);
            gym.Id = maxId + 1;
            InMemGyms.Add(gym);
            return gym;
        }
    }

    public async Task<IReadOnlyList<GymRosterMemberDto>> GetGymRosterAsync(int? gymId, int? divisionId)
    {
        _logger.LogDebug("Loading gym roster (gymId={GymId}, divisionId={DivisionId})", gymId, divisionId);

        var fallbackGyms = AffiliationSeedData.CreateFallbackGyms().ToDictionary(g => g.Id, g => g.Name);
        var fallbackDivisions = AffiliationSeedData.CreateDivisions().ToDictionary(d => d.Id, d => d.Name);

        try
        {
            var query = _db.Users
                .AsNoTracking()
                .Include(u => u.Gym)
                .Include(u => u.Division)
                .Include(u => u.UserProgress)
                .AsQueryable();

            if (gymId.HasValue)
            {
                query = query.Where(u => u.GymId == gymId.Value);
            }

            if (divisionId.HasValue)
            {
                query = query.Where(u => u.DivisionId == divisionId.Value);
            }

            var users = await query
                .OrderByDescending(u => u.UserProgress != null ? u.UserProgress.Xp : 0)
                .ThenBy(u => u.Username)
                .ToListAsync();

            var suspiciousByUser = await _db.WorkoutRecords
                .AsNoTracking()
                .Where(r => r.IsSuspicious && r.Session != null && r.Session.Workout != null)
                .GroupBy(r => r.Session!.Workout!.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var list = new List<GymRosterMemberDto>(users.Count);
            foreach (var u in users)
            {
                var gymName = u.Gym?.Name
                    ?? (u.GymId.HasValue && fallbackGyms.TryGetValue(u.GymId.Value, out var fGym) ? fGym : "Не обрано");
                var divName = u.Division?.Name
                    ?? (u.DivisionId.HasValue && fallbackDivisions.TryGetValue(u.DivisionId.Value, out var fDiv) ? fDiv : "Не обрано");
                suspiciousByUser.TryGetValue(u.Id, out var suspiciousCount);

                list.Add(new GymRosterMemberDto
                {
                    UserId = (int)u.Id,
                    Nickname = u.Username ?? string.Empty,
                    Role = string.Equals(u.Username, "sazerwar", StringComparison.OrdinalIgnoreCase)
                        ? UserRole.Admin.ToString()
                        : u.Role.ToString(),
                    GymId = u.GymId,
                    GymName = gymName,
                    DivisionId = u.DivisionId,
                    DivisionName = divName,
                    Level = u.UserProgress?.Level ?? 1,
                    Xp = (int)(u.UserProgress?.Xp ?? 0),
                    SuspiciousWorkoutsCount = suspiciousCount,
                    IsExcludedFromLeaderboard = u.IsExcludedFromLeaderboard,
                    IsBlocked = u.IsBlocked,
                    BlockedAt = u.BlockedAt,
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка завантаження складу спортзалу з БД, використовуємо локальний список");
        }

        lock (InMemUsers)
        {
            IEnumerable<User> localQuery = InMemUsers;
            if (gymId.HasValue)
            {
                localQuery = localQuery.Where(u => u.GymId == gymId.Value);
            }

            if (divisionId.HasValue)
            {
                localQuery = localQuery.Where(u => u.DivisionId == divisionId.Value);
            }

            return localQuery
                .Select(u => new GymRosterMemberDto
                {
                    UserId = (int)u.Id,
                    Nickname = u.Username ?? string.Empty,
                    Role = string.Equals(u.Username, "sazerwar", StringComparison.OrdinalIgnoreCase)
                        ? UserRole.Admin.ToString()
                        : u.Role.ToString(),
                    GymId = u.GymId,
                    GymName = u.GymId.HasValue && fallbackGyms.TryGetValue(u.GymId.Value, out var gName) ? gName : "Не обрано",
                    DivisionId = u.DivisionId,
                    DivisionName = u.DivisionId.HasValue && fallbackDivisions.TryGetValue(u.DivisionId.Value, out var dName) ? dName : "Не обрано",
                    Level = u.UserProgress?.Level ?? 1,
                    Xp = (int)(u.UserProgress?.Xp ?? 0),
                    SuspiciousWorkoutsCount = 0,
                    IsExcludedFromLeaderboard = u.IsExcludedFromLeaderboard,
                    IsBlocked = u.IsBlocked,
                    BlockedAt = u.BlockedAt,
                })
                .ToList();
        }
    }

    public async Task<bool> UpdateModerationStatusAsync(
        int userId,
        bool isExcludedFromLeaderboard,
        bool isBlocked,
        DateTimeOffset? blockedAt)
    {
        _logger.LogDebug(
            "Updating moderation status for user {UserId}: isExcluded={IsExcluded}, isBlocked={IsBlocked}",
            userId,
            isExcludedFromLeaderboard,
            isBlocked);

        User? tracked = null;
        try
        {
            tracked = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (tracked is not null)
            {
                tracked.IsExcludedFromLeaderboard = isExcludedFromLeaderboard;
                tracked.IsBlocked = isBlocked;
                tracked.BlockedAt = blockedAt;
                tracked.UpdatedAt = DateTimeOffset.UtcNow;

                await _db.SaveChangesAsync();

                lock (InMemUsers)
                {
                    var localCopy = InMemUsers.Find(u => u.Id == userId);
                    if (localCopy is not null)
                    {
                        localCopy.IsExcludedFromLeaderboard = isExcludedFromLeaderboard;
                        localCopy.IsBlocked = isBlocked;
                        localCopy.BlockedAt = blockedAt;
                        localCopy.UpdatedAt = tracked.UpdatedAt;
                    }
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка оновлення статусу модерації {UserId} в БД, оновлюємо локальний список", userId);
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

            local.IsExcludedFromLeaderboard = isExcludedFromLeaderboard;
            local.IsBlocked = isBlocked;
            local.BlockedAt = blockedAt;
            local.UpdatedAt = DateTimeOffset.UtcNow;
            return true;
        }
    }

    internal static IReadOnlyList<User> GetInMemoryUsersSnapshot()
    {
        lock (InMemUsers)
        {
            return InMemUsers.ToList();
        }
    }

    private static List<Gym> GetFallbackGyms()
    {
        var gyms = AffiliationSeedData.CreateFallbackGyms().ToList();

        lock (InMemGyms)
        {
            gyms.AddRange(InMemGyms);
        }

        return gyms;
    }

    private static List<GymClubStatsDto> BuildClubStats(IReadOnlyList<GymRow> gyms, IReadOnlyList<AthleteRow> athletes)
    {
        var byGym = athletes.ToLookup(a => a.GymId);

        return gyms
            .Select(g =>
            {
                var members = byGym[g.Id].ToList();
                var leader = members
                    .OrderByDescending(m => m.Xp)
                    .ThenBy(m => m.Username, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                return new GymClubStatsDto(
                    g.Id,
                    g.Name,
                    g.RegionId,
                    g.RegionName,
                    members.Count,
                    members.Sum(m => m.Xp),
                    leader?.Username);
            })
            .ToList();
    }

    private static List<GymClubStatsDto> BuildFallbackClubStats(int? regionId, int? divisionId)
    {
        var regionNames = AffiliationSeedData.CreateFallbackRegions().ToDictionary(r => r.Id, r => r.Name);

        var gyms = GetFallbackGyms()
            .Where(g => regionId is null || g.RegionId == regionId)
            .Select(g => new GymRow(
                g.Id,
                g.Name,
                g.RegionId,
                regionNames.GetValueOrDefault(g.RegionId, string.Empty)))
            .ToList();

        List<AthleteRow> athletes;
        lock (InMemUsers)
        {
            athletes = InMemUsers
                .Where(u => u.GymId != null
                    && !u.IsBlocked
                    && !u.IsExcludedFromLeaderboard
                    && (divisionId is null || u.DivisionId == divisionId))
                .Select(u => new AthleteRow(u.GymId!.Value, u.Username, u.UserProgress?.Xp ?? 0L))
                .ToList();
        }

        return BuildClubStats(gyms, athletes);
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

    private sealed record GymRow(int Id, string Name, int RegionId, string RegionName);

    private sealed record AthleteRow(int GymId, string Username, long Xp);
}
