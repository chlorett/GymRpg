using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class WorkoutRecordRepository : IWorkoutRecordRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<WorkoutRecordRepository> _logger;

    public WorkoutRecordRepository(AppDbContext db, ILogger<WorkoutRecordRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AddAsync(WorkoutRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        _logger.LogDebug("Saving workout record for exercise {ExerciseId}", record.ExerciseId);

        _db.Set<WorkoutRecord>().Add(record);
        await _db.SaveChangesAsync(ct);
    }
}
