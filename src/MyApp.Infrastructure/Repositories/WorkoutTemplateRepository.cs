using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class WorkoutTemplateRepository : IWorkoutTemplateRepository
{
    private static readonly List<WorkoutTemplate> InMemTemplates = new();
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

        try
        {
            _db.Set<WorkoutTemplate>().Add(template);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка збереження шаблону в БД, зберігаємо в пам'ять");
            lock (InMemTemplates)
            {
                if (template.Id == 0)
                {
                    template.Id = InMemTemplates.Count + 1;
                }

                InMemTemplates.Add(template);
            }
        }
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