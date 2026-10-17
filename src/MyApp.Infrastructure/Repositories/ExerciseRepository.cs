using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class ExerciseRepository : IExerciseRepository
{
    private static readonly IReadOnlyList<Exercise> DefaultExercises = new List<Exercise>
    {
        new() { Id = 1, Name = "Жим лежачи на лаві", Description = "Базова вправа для розвитку грудних м'язів, трицепсів та передньої дельти." },
        new() { Id = 2, Name = "Присідання зі штангою", Description = "Основна вправа для розвитку квадрицепсів, сідниць та м'язів кора." },
        new() { Id = 3, Name = "Станова тяга", Description = "Фундаментальна вправа для спини, біцепсів стегон та трапецій." },
        new() { Id = 4, Name = "Підтягування на перекладині", Description = "Вправа з власною вагою для найширших м'язів спини та біцепсів." },
        new() { Id = 5, Name = "Армійський жим", Description = "Жим штанги стоячи для розвитку плечового пояса та дельт." },
        new() { Id = 6, Name = "Віджимання на брусах", Description = "Вправа для нижньої частини грудей та трицепсів." },
        new() { Id = 7, Name = "Тяга штанги в нахилі", Description = "Вправа для товщини м'язів спини." },
        new() { Id = 8, Name = "Згинання рук зі штангою", Description = "Ізолююча вправа для біцепсів." }
    };

    private readonly AppDbContext _db;
    private readonly ILogger<ExerciseRepository> _logger;

    public ExerciseRepository(AppDbContext db, ILogger<ExerciseRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Exercise?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _logger.LogDebug("Loading exercise {ExerciseId}", id);

        try
        {
            var exercise = await _db.Exercises
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);

            if (exercise is not null)
            {
                return exercise;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка зв'язку з базою даних при пошуку вправи {ExerciseId}, перевіряємо резервний каталог", id);
        }

        return DefaultExercises.FirstOrDefault(e => e.Id == id);
    }

    public async Task<IReadOnlyList<Exercise>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Loading all exercises");

        try
        {
            var dbExercises = await _db.Exercises
                .AsNoTracking()
                .Where(exercise => !exercise.IsDeleted)
                .OrderBy(exercise => exercise.Name)
                .ToListAsync(ct);

            if (dbExercises.Count > 0)
            {
                return dbExercises;
            }

            _logger.LogWarning("База даних не містить вправ, використовуємо базовий каталог");
            return DefaultExercises;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка зв'язку з базою даних при завантаженні вправ, використовуємо резервний каталог");
            return DefaultExercises;
        }
    }
}