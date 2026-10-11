using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class WorkoutTemplateRepository : IWorkoutTemplateRepository
{
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

        return await _db.Set<WorkoutTemplate>()
            .AsNoTracking()
            .AnyAsync(
                template => template.TrainerId == trainerId && EF.Functions.ILike(template.Name, pattern),
                ct);
    }

    public async Task AddAsync(WorkoutTemplate template, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        _logger.LogDebug(
            "Adding workout template {Name} for trainer {TrainerId}",
            template.Name,
            template.TrainerId);

        _db.Set<WorkoutTemplate>().Add(template);
        await _db.SaveChangesAsync(ct);
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