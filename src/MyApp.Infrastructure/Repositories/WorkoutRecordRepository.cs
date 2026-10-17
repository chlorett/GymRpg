using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class WorkoutRecordRepository : IWorkoutRecordRepository
{
    private static readonly List<WorkoutRecord> InMemRecords = new();
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

        try
        {
            _db.Set<WorkoutRecord>().Add(record);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка збереження тренування в БД, зберігаємо в пам'ять");
            lock (InMemRecords)
            {
                if (record.Id == 0)
                {
                    record.Id = InMemRecords.Count + 1;
                }

                InMemRecords.Add(record);
            }
        }
    }
}
