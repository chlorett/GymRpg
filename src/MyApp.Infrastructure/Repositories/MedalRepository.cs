using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class MedalRepository : IMedalRepository
{
    private static readonly IReadOnlyList<Medal> DefaultMedals = new List<Medal>
    {
        new()
        {
            Id = 1,
            Name = "Перший крок у залі",
            Description = "Зафіксувати своє перше виконане тренування у системі.",
            ConditionType = MedalConditionType.WorkoutsCount,
            ConditionValue = 1,
        },
        new()
        {
            Id = 2,
            Name = "Залізна дисципліна",
            Description = "Виконати 5 повноцінних тренувань без порушень.",
            ConditionType = MedalConditionType.WorkoutsCount,
            ConditionValue = 5,
        },
        new()
        {
            Id = 3,
            Name = "Новий рубіж (Рівень 2)",
            Description = "Досягти 2-го рівня розвитку персонажа.",
            ConditionType = MedalConditionType.Level,
            ConditionValue = 2,
        },
        new()
        {
            Id = 4,
            Name = "Залізний аматор (Рівень 3)",
            Description = "Піднятися до 3-го рівня персонажа завдяки регулярним тренуванням.",
            ConditionType = MedalConditionType.Level,
            ConditionValue = 3,
        },
        new()
        {
            Id = 5,
            Name = "Досвідчений атлет (Рівень 5)",
            Description = "Досягти 5-го рівня персонажа у PulseForge.",
            ConditionType = MedalConditionType.Level,
            ConditionValue = 5,
        },
        new()
        {
            Id = 6,
            Name = "Перша тисяча XP",
            Description = "Накопичити 1 000 очок досвіду (XP).",
            ConditionType = MedalConditionType.TotalXp,
            ConditionValue = 1000,
        },
        new()
        {
            Id = 7,
            Name = "Енергія PulseForge",
            Description = "Набрати 2 500 очок досвіду (XP) за виконання силових вправ.",
            ConditionType = MedalConditionType.TotalXp,
            ConditionValue = 2500,
        },
        new()
        {
            Id = 8,
            Name = "Серія перемог",
            Description = "Провести тренування у 3 різних активних дні.",
            ConditionType = MedalConditionType.StreakDays,
            ConditionValue = 3,
        },
    };

    private static readonly List<UserMedal> InMemUserMedals = new();

    private readonly AppDbContext _db;
    private readonly ILogger<MedalRepository> _logger;

    public MedalRepository(AppDbContext db, ILogger<MedalRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<Medal>> GetAllMedalsAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Loading all medals from catalog");

        try
        {
            var dbMedals = await _db.Medals
                .AsNoTracking()
                .OrderBy(m => m.Id)
                .ToListAsync(ct);

            if (dbMedals.Count > 0)
            {
                return dbMedals;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка завантаження медалей з БД, використовуємо резервний довідник");
        }

        return DefaultMedals;
    }

    public async Task<IReadOnlyList<UserMedal>> GetUserMedalsAsync(int userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Loading earned medals for user {UserId}", userId);

        try
        {
            var dbUserMedals = await _db.UserMedals
                .AsNoTracking()
                .Include(um => um.Medal)
                .Where(um => um.UserId == userId)
                .OrderByDescending(um => um.EarnedAt)
                .ToListAsync(ct);

            if (dbUserMedals.Count > 0)
            {
                return dbUserMedals;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка читання нагород користувача {UserId} з БД, перевіряємо локальний кеш", userId);
        }

        lock (InMemUserMedals)
        {
            return InMemUserMedals
                .Where(um => um.UserId == userId)
                .OrderByDescending(um => um.EarnedAt)
                .ToList();
        }
    }

    public async Task AddUserMedalsAsync(IReadOnlyList<UserMedal> userMedals, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userMedals);
        if (userMedals.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Saving {Count} earned medal(s)", userMedals.Count);

        try
        {
            _db.UserMedals.AddRange(userMedals);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка збереження нагород у БД, зберігаємо у локальний кеш");
        }

        lock (InMemUserMedals)
        {
            foreach (var item in userMedals.Where(item =>
                !InMemUserMedals.Exists(existing => existing.UserId == item.UserId && existing.MedalId == item.MedalId)))
            {
                if (item.Id == 0)
                {
                    item.Id = InMemUserMedals.Count + 1;
                }

                InMemUserMedals.Add(item);
            }
        }
    }

    internal static IReadOnlyList<Medal> GetDefaultMedals() => DefaultMedals;
}
