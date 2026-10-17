using Microsoft.EntityFrameworkCore;
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

        bool dbSaved = false;
        try
        {
            _db.Set<WorkoutRecord>().Add(record);
            await _db.SaveChangesAsync(ct);
            dbSaved = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка збереження тренування в БД, зберігаємо в пам'ять");
        }

        lock (InMemRecords)
        {
            if (!dbSaved && record.Id == 0)
            {
                record.Id = InMemRecords.Count + 1;
            }

            InMemRecords.Add(record);
        }
    }

    public async Task<IReadOnlyList<WorkoutRecord>> GetRecentByUserIdAsync(
        int userId,
        int count = 10,
        CancellationToken ct = default)
    {
        try
        {
            var records = await _db.Set<WorkoutRecord>()
                .Include(r => r.Exercise)
                .Include(r => r.WorkoutSets)
                .Include(r => r.Session)
                    .ThenInclude(s => s.Workout)
                .Where(r => r.Session.Workout.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(count)
                .AsNoTracking()
                .ToListAsync(ct);

            if (records.Count > 0)
            {
                return records;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка отримання історії тренувань з БД для користувача {UserId}, читаємо з пам'яті", userId);
        }

        lock (InMemRecords)
        {
            return InMemRecords
                .Where(r => r.Session?.Workout?.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(count)
                .ToList();
        }
    }
}
