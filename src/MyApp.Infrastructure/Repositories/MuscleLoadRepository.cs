using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class MuscleLoadRepository : IMuscleLoadRepository
{
    private static readonly object SyncLock = new();
    private static readonly IReadOnlyList<MuscleGroup> DefaultMuscleGroups = CreateDefaultMuscleGroups();
    private static readonly IReadOnlyList<ExerciseMuscleGroup> DefaultExerciseMuscleGroups = CreateDefaultExerciseMuscleGroups();
    private static readonly List<MuscleLoad> InMemoryMuscleLoads = CreateDefaultUserMuscleLoads();

    private readonly AppDbContext _db;
    private readonly ILogger<MuscleLoadRepository> _logger;

    public MuscleLoadRepository(AppDbContext db, ILogger<MuscleLoadRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static IReadOnlyList<MuscleGroup> GetDefaultMuscleGroups() => DefaultMuscleGroups;

    public static IReadOnlyList<ExerciseMuscleGroup> GetDefaultExerciseMuscleGroups() => DefaultExerciseMuscleGroups;

    public async Task<IReadOnlyList<MuscleGroup>> GetAllMuscleGroupsAsync(CancellationToken ct = default)
    {
        try
        {
            var groups = await _db.MuscleGroups
                .AsNoTracking()
                .OrderBy(g => g.Id)
                .ToListAsync(ct);

            if (groups.Count > 0)
            {
                return groups;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не вдалося завантажити групи м'язів із PostgreSQL, використовуємо вбудований довідник");
        }

        return DefaultMuscleGroups;
    }

    public async Task<IReadOnlyList<ExerciseMuscleGroup>> GetAllExerciseMuscleGroupsAsync(CancellationToken ct = default)
    {
        try
        {
            var links = await _db.ExerciseMuscleGroups
                .AsNoTracking()
                .OrderBy(l => l.ExerciseId)
                .ThenBy(l => l.MuscleGroupId)
                .ToListAsync(ct);

            if (links.Count > 0)
            {
                return links;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не вдалося завантажити зв'язки вправ і груп м'язів із PostgreSQL, використовуємо резервну матрицю");
        }

        return DefaultExerciseMuscleGroups;
    }

    public async Task<IReadOnlyList<MuscleLoad>> GetUserMuscleLoadsAsync(
        int userId,
        CancellationToken ct = default)
    {
        try
        {
            var loads = await _db.MuscleLoads
                .AsNoTracking()
                .Where(l => l.UserId == userId)
                .OrderBy(l => l.MuscleGroupId)
                .ToListAsync(ct);

            if (loads.Count > 0)
            {
                return loads;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не вдалося завантажити навантаження м'язів користувача {UserId} з БД, використовуємо резервний список",
                userId);
        }

        lock (SyncLock)
        {
            return InMemoryMuscleLoads
                .Where(l => l.UserId == userId)
                .OrderBy(l => l.MuscleGroupId)
                .ToList();
        }
    }

    public async Task SaveUserMuscleLoadsAsync(
        int userId,
        IReadOnlyList<MuscleLoad> loads,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(loads);

        if (loads.Count == 0)
        {
            return;
        }

        try
        {
            var existing = await _db.MuscleLoads
                .Where(l => l.UserId == userId)
                .ToListAsync(ct);

            var existingByGroup = existing.ToDictionary(l => l.MuscleGroupId);
            foreach (var item in loads)
            {
                if (existingByGroup.TryGetValue(item.MuscleGroupId, out var current))
                {
                    current.LoadValue = item.LoadValue;
                    current.UpdatedAt = item.UpdatedAt;
                }
                else
                {
                    _db.MuscleLoads.Add(new MuscleLoad
                    {
                        UserId = userId,
                        MuscleGroupId = item.MuscleGroupId,
                        LoadValue = item.LoadValue,
                        UpdatedAt = item.UpdatedAt,
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не вдалося зберегти розрахунок м'язового навантаження для користувача {UserId} в PostgreSQL, зберігаємо локально",
                userId);
        }

        lock (SyncLock)
        {
            InMemoryMuscleLoads.RemoveAll(l => l.UserId == userId);
            foreach (var item in loads)
            {
                InMemoryMuscleLoads.Add(new MuscleLoad
                {
                    Id = InMemoryMuscleLoads.Count + 1,
                    UserId = userId,
                    MuscleGroupId = item.MuscleGroupId,
                    LoadValue = item.LoadValue,
                    UpdatedAt = item.UpdatedAt,
                });
            }
        }
    }

    private static List<MuscleGroup> CreateDefaultMuscleGroups()
    {
        return new List<MuscleGroup>
        {
            new() { Id = 1, Name = "Грудні м'язи" },
            new() { Id = 2, Name = "М'язи спини" },
            new() { Id = 3, Name = "М'язи ніг та сідниць" },
            new() { Id = 4, Name = "Плечовий пояс (Дельти)" },
            new() { Id = 5, Name = "М'язи рук (Біцепс і Трицепс)" },
            new() { Id = 6, Name = "М'язи кора та преса" },
        };
    }

    private static List<ExerciseMuscleGroup> CreateDefaultExerciseMuscleGroups()
    {
        return new List<ExerciseMuscleGroup>
        {
            // 1. Жим лежачи на лаві: Груди (1.0), Руки/Трицепс (0.5), Плечі (0.4)
            new() { ExerciseId = 1, MuscleGroupId = 1, LoadFactor = 1.000m },
            new() { ExerciseId = 1, MuscleGroupId = 5, LoadFactor = 0.500m },
            new() { ExerciseId = 1, MuscleGroupId = 4, LoadFactor = 0.400m },

            // 2. Присідання зі штангою: Ноги (1.0), Кор (0.5)
            new() { ExerciseId = 2, MuscleGroupId = 3, LoadFactor = 1.000m },
            new() { ExerciseId = 2, MuscleGroupId = 6, LoadFactor = 0.500m },

            // 3. Станова тяга: Спина (1.0), Ноги (0.7), Кор (0.5)
            new() { ExerciseId = 3, MuscleGroupId = 2, LoadFactor = 1.000m },
            new() { ExerciseId = 3, MuscleGroupId = 3, LoadFactor = 0.700m },
            new() { ExerciseId = 3, MuscleGroupId = 6, LoadFactor = 0.500m },

            // 4. Підтягування на перекладині: Спина (1.0), Руки/Біцепс (0.6)
            new() { ExerciseId = 4, MuscleGroupId = 2, LoadFactor = 1.000m },
            new() { ExerciseId = 4, MuscleGroupId = 5, LoadFactor = 0.600m },

            // 5. Армійський жим: Плечі (1.0), Руки/Трицепс (0.5), Кор (0.3)
            new() { ExerciseId = 5, MuscleGroupId = 4, LoadFactor = 1.000m },
            new() { ExerciseId = 5, MuscleGroupId = 5, LoadFactor = 0.500m },
            new() { ExerciseId = 5, MuscleGroupId = 6, LoadFactor = 0.300m },

            // 6. Віджимання на брусах: Груди (0.8), Руки/Трицепс (0.8), Плечі (0.4)
            new() { ExerciseId = 6, MuscleGroupId = 1, LoadFactor = 0.800m },
            new() { ExerciseId = 6, MuscleGroupId = 5, LoadFactor = 0.800m },
            new() { ExerciseId = 6, MuscleGroupId = 4, LoadFactor = 0.400m },

            // 7. Тяга штанги в нахилі: Спина (1.0), Руки/Біцепс (0.5)
            new() { ExerciseId = 7, MuscleGroupId = 2, LoadFactor = 1.000m },
            new() { ExerciseId = 7, MuscleGroupId = 5, LoadFactor = 0.500m },

            // 8. Згинання рук зі штангою: Руки/Біцепс (1.0)
            new() { ExerciseId = 8, MuscleGroupId = 5, LoadFactor = 1.000m },

            // 9. Віджимання / Жим гантелей: Груди (1.0), Плечі (0.5), Руки (0.4)
            new() { ExerciseId = 9, MuscleGroupId = 1, LoadFactor = 1.000m },
            new() { ExerciseId = 9, MuscleGroupId = 4, LoadFactor = 0.500m },
            new() { ExerciseId = 9, MuscleGroupId = 5, LoadFactor = 0.400m },

            // 10. Підйом ніг у висі / Румунська тяга: Кор (0.8), Ноги (0.8)
            new() { ExerciseId = 10, MuscleGroupId = 6, LoadFactor = 0.800m },
            new() { ExerciseId = 10, MuscleGroupId = 3, LoadFactor = 0.800m },
        };
    }

    private static List<MuscleLoad> CreateDefaultUserMuscleLoads()
    {
        var now = DateTimeOffset.UtcNow.AddHours(-2);
        return new List<MuscleLoad>
        {
            new() { Id = 1, UserId = 1, MuscleGroupId = 1, LoadValue = 320.0m, UpdatedAt = now },
            new() { Id = 2, UserId = 1, MuscleGroupId = 2, LoadValue = 280.0m, UpdatedAt = now },
            new() { Id = 3, UserId = 1, MuscleGroupId = 3, LoadValue = 210.0m, UpdatedAt = now },
            new() { Id = 4, UserId = 1, MuscleGroupId = 4, LoadValue = 145.0m, UpdatedAt = now },
            new() { Id = 5, UserId = 1, MuscleGroupId = 5, LoadValue = 190.0m, UpdatedAt = now },
            new() { Id = 6, UserId = 1, MuscleGroupId = 6, LoadValue = 65.0m, UpdatedAt = now },
        };
    }
}
