using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class ExerciseRepository : IExerciseRepository
{
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

        return await _db.Exercises
            .AsNoTracking()
            .FirstOrDefaultAsync(exercise => exercise.Id == id && !exercise.IsDeleted, ct);
    }

    public async Task<IReadOnlyList<Exercise>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Loading all exercises");

        return await _db.Exercises
            .AsNoTracking()
            .Where(exercise => !exercise.IsDeleted)
            .OrderBy(exercise => exercise.Name)
            .ToListAsync(ct);
    }
}