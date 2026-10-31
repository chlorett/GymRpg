using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class WorkoutTemplateRepository : IWorkoutTemplateRepository
{
    private static readonly List<WorkoutTemplate> InMemTemplates = CreateDefaultTemplates();
    private readonly AppDbContext _db;
    private readonly ILogger<WorkoutTemplateRepository> _logger;

    public WorkoutTemplateRepository(AppDbContext db, ILogger<WorkoutTemplateRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> ExistsByNameAsync(int trainerId, string name, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        _logger.LogDebug("Checking template name for trainer {TrainerId}", trainerId);

        var pattern = EscapeLikePattern(name);

        try
        {
            var exists = await _db.Set<WorkoutTemplate>()
                .AsNoTracking()
                .AnyAsync(
                    template => template.TrainerId == trainerId && EF.Functions.ILike(template.Name, pattern),
                    ct);

            if (exists)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка перевірки назви шаблону в БД, перевіряємо локальний список");
        }

        lock (InMemTemplates)
        {
            return InMemTemplates.Exists(t => t.TrainerId == trainerId && t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    public async Task AddAsync(WorkoutTemplate template, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        _logger.LogDebug(
            "Adding workout template {Name} for trainer {TrainerId}",
            template.Name,
            template.TrainerId);

        bool savedToDb = false;
        try
        {
            _db.Set<WorkoutTemplate>().Add(template);
            await _db.SaveChangesAsync(ct);
            savedToDb = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка збереження шаблону в БД, зберігаємо в пам'ять");
        }

        lock (InMemTemplates)
        {
            if (!savedToDb && template.Id == 0)
            {
                long nextId = InMemTemplates.Count == 0 ? 1L : InMemTemplates.Max(t => t.Id) + 1L;
                template.Id = nextId;
            }

            InMemTemplates.RemoveAll(t => t.Id == template.Id && template.Id != 0);
            InMemTemplates.Add(template);
        }
    }

    public async Task<IReadOnlyList<WorkoutTemplate>> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Loading workout templates for trainer {UserId}", userId);

        try
        {
            var dbTemplates = await _db.Set<WorkoutTemplate>()
                .AsNoTracking()
                .Include(t => t.WorkoutTemplateExercises)
                    .ThenInclude(te => te.Exercise)
                .Where(t => t.TrainerId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(ct);

            if (dbTemplates.Count > 0)
            {
                return dbTemplates;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка отримання шаблонів користувача {UserId} з БД, читаємо з локального списку", userId);
        }

        lock (InMemTemplates)
        {
            return InMemTemplates
                .Where(t => t.TrainerId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToList();
        }
    }

    public async Task<WorkoutTemplate?> GetByIdWithExercisesAsync(int templateId, CancellationToken ct = default)
    {
        _logger.LogDebug("Loading workout template details for template {TemplateId}", templateId);

        try
        {
            var dbTemplate = await _db.Set<WorkoutTemplate>()
                .AsNoTracking()
                .Include(t => t.WorkoutTemplateExercises)
                    .ThenInclude(te => te.Exercise)
                .FirstOrDefaultAsync(t => t.Id == templateId, ct);

            if (dbTemplate is not null)
            {
                return dbTemplate;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка отримання деталей шаблону {TemplateId} з БД, перевіряємо локальний список", templateId);
        }

        lock (InMemTemplates)
        {
            return InMemTemplates.Find(t => t.Id == templateId);
        }
    }

    public async Task DeleteAsync(int templateId, CancellationToken ct = default)
    {
        _logger.LogDebug("Deleting workout template {TemplateId}", templateId);

        try
        {
            var entity = await _db.Set<WorkoutTemplate>()
                .Include(t => t.WorkoutTemplateExercises)
                .FirstOrDefaultAsync(t => t.Id == templateId, ct);

            if (entity is not null)
            {
                _db.Set<WorkoutTemplateExercise>().RemoveRange(entity.WorkoutTemplateExercises);
                _db.Set<WorkoutTemplate>().Remove(entity);
                await _db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка видалення шаблону {TemplateId} з БД, видаляємо з локального списку", templateId);
        }

        lock (InMemTemplates)
        {
            InMemTemplates.RemoveAll(t => t.Id == templateId);
        }
    }

    private static List<WorkoutTemplate> CreateDefaultTemplates()
    {
        var now = DateTimeOffset.UtcNow;
        return new List<WorkoutTemplate>
        {
            new()
            {
                Id = 1,
                TrainerId = 2,
                TrainerRole = UserRole.Trainer,
                Name = "Базовий силовий спліт (Верх тіла)",
                Description = "Класична програма для розвитку сили грудних м'язів, спини та плечей.",
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-5),
                WorkoutTemplateExercises = new List<WorkoutTemplateExercise>
                {
                    new()
                    {
                        Id = 1,
                        TemplateId = 1,
                        ExerciseId = 1,
                        Position = 1,
                        TargetSets = 4,
                        TargetReps = 8,
                        Exercise = new Exercise
                        {
                            Id = 1,
                            Name = "Жим лежачи на лаві",
                            EquipmentType = EquipmentType.Gym,
                        },
                    },
                    new()
                    {
                        Id = 2,
                        TemplateId = 1,
                        ExerciseId = 4,
                        Position = 2,
                        TargetSets = 4,
                        TargetReps = 10,
                        Exercise = new Exercise
                        {
                            Id = 4,
                            Name = "Підтягування на перекладині",
                            EquipmentType = EquipmentType.Calisthenics,
                        },
                    },
                    new()
                    {
                        Id = 3,
                        TemplateId = 1,
                        ExerciseId = 5,
                        Position = 3,
                        TargetSets = 3,
                        TargetReps = 10,
                        Exercise = new Exercise
                        {
                            Id = 5,
                            Name = "Армійський жим",
                            EquipmentType = EquipmentType.Gym,
                        },
                    },
                },
            },
            new()
            {
                Id = 2,
                TrainerId = 2,
                TrainerRole = UserRole.Trainer,
                Name = "Функціональний фулбоді та кор",
                Description = "Поєднання базових вправ зі штангою та калістеніки для витривалості.",
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-2),
                WorkoutTemplateExercises = new List<WorkoutTemplateExercise>
                {
                    new()
                    {
                        Id = 4,
                        TemplateId = 2,
                        ExerciseId = 2,
                        Position = 1,
                        TargetSets = 4,
                        TargetReps = 10,
                        Exercise = new Exercise
                        {
                            Id = 2,
                            Name = "Присідання зі штангою",
                            EquipmentType = EquipmentType.Gym,
                        },
                    },
                    new()
                    {
                        Id = 5,
                        TemplateId = 2,
                        ExerciseId = 6,
                        Position = 2,
                        TargetSets = 3,
                        TargetReps = 12,
                        Exercise = new Exercise
                        {
                            Id = 6,
                            Name = "Віджимання на брусах",
                            EquipmentType = EquipmentType.Calisthenics,
                        },
                    },
                    new()
                    {
                        Id = 6,
                        TemplateId = 2,
                        ExerciseId = 10,
                        Position = 3,
                        TargetSets = 3,
                        TargetReps = 15,
                        Exercise = new Exercise
                        {
                            Id = 10,
                            Name = "Підйом ніг у висі",
                            EquipmentType = EquipmentType.Calisthenics,
                        },
                    },
                },
            },
        };
    }

    // ILIKE без підстановок: екрануємо \, % і _, щоб порівняння було точним, але без урахування регістру.
    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
