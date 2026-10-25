using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class ExerciseRepository : IExerciseRepository
{
    private static readonly IReadOnlyList<Exercise> DefaultExercises = new List<Exercise>
    {
        new()
        {
            Id = 1,
            Name = "Жим лежачи на лаві",
            EquipmentType = EquipmentType.Gym,
            Description = "Базова вправа для розвитку грудних м'язів, трицепсів та передньої дельти.",
            Technique = "Ляжте на лаву, зведіть лопатки, опустіть штангу до середини грудей і потужно витисніть угору.",
        },
        new()
        {
            Id = 2,
            Name = "Присідання зі штангою",
            EquipmentType = EquipmentType.Gym,
            Description = "Основна вправа для розвитку квадрицепсів, сідниць та м'язів кора.",
            Technique = "Тримайте спину рівною, відводьте таз назад і присідайте до паралелі стегон із підлогою.",
        },
        new()
        {
            Id = 3,
            Name = "Станова тяга",
            EquipmentType = EquipmentType.Gym,
            Description = "Фундаментальна вправа для спини, біцепсів стегон та трапецій.",
            Technique = "Підійдіть впритул до грифа, тримайте спину нейтральною, піднімайте штангу за рахунок ніг і розгинання корпусу.",
        },
        new()
        {
            Id = 4,
            Name = "Підтягування на перекладині",
            EquipmentType = EquipmentType.Calisthenics,
            Description = "Вправа з власною вагою для найширших м'язів спини та біцепсів.",
            Technique = "Візьміться за перекладину хватом трохи ширше плечей, тягніться грудьми до перекладини, опускайтеся підконтрольно.",
        },
        new()
        {
            Id = 5,
            Name = "Армійський жим",
            EquipmentType = EquipmentType.Gym,
            Description = "Жим штанги стоячи для розвитку плечового пояса та дельт.",
            Technique = "Напружте прес і сідниці, витисніть штангу з грудей вертикально над головою без прогину в попереку.",
        },
        new()
        {
            Id = 6,
            Name = "Віджимання на брусах",
            EquipmentType = EquipmentType.Calisthenics,
            Description = "Вправа з власною вагою для нижньої частини грудей та трицепсів.",
            Technique = "Нахиліть корпус трохи вперед, опускайтеся до кута 90 градусів у ліктях і плавно витискайте себе вгору.",
        },
        new()
        {
            Id = 7,
            Name = "Тяга штанги в нахилі",
            EquipmentType = EquipmentType.Gym,
            Description = "Вправа для товщини м'язів спини.",
            Technique = "Нахиліть корпус під кутом 45 градусів, тягніть штангу до низу живота, зводячи лопатки у верхній точці.",
        },
        new()
        {
            Id = 8,
            Name = "Згинання рук зі штангою",
            EquipmentType = EquipmentType.Gym,
            Description = "Ізолююча вправа для біцепсів.",
            Technique = "Тримайте лікті притиснутими до тулуба, піднімайте штангу за рахунок скорочення біцепсів без розгойдування.",
        },
        new()
        {
            Id = 9,
            Name = "Віджимання від підлоги",
            EquipmentType = EquipmentType.Calisthenics,
            Description = "Класична вправа з власною вагою для грудних м'язів, трицепсів і кора.",
            Technique = "Тримайте тіло в одній прямій лінії, опускайтеся грудьми майже до підлоги і розгинайте руки.",
        },
        new()
        {
            Id = 10,
            Name = "Підйом ніг у висі",
            EquipmentType = EquipmentType.Calisthenics,
            Description = "Ефективна калістенічна вправа для прямого та косих м'язів живота.",
            Technique = "Повісьте на перекладині, піднімайте прямі або зігнуті в колінах ноги до рівня паралелі з підлогою.",
        },
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