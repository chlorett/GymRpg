using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class PersonalRecordRepository : IPersonalRecordRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<PersonalRecordRepository> _logger;

    public PersonalRecordRepository(AppDbContext db, ILogger<PersonalRecordRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<WorkoutRecord>> GetAllByUserIdAsync(
        int userId,
        CancellationToken ct = default)
    {
        try
        {
            var records = await _db.WorkoutRecords
                .Include(r => r.Exercise)
                .Include(r => r.WorkoutSets)
                .Include(r => r.Session)
                    .ThenInclude(s => s.Workout)
                .Where(r => r.Session.Workout.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .AsNoTracking()
                .ToListAsync(ct);

            if (records.Count > 0)
            {
                return records;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Помилка отримання повного журналу тренувань з БД для користувача {UserId}, використовуємо локальну пам'ять",
                userId);
        }

        return WorkoutRecordRepository.GetInMemoryByUserId(userId);
    }

    public async Task<WorkoutRecord?> GetByIdWithDetailsAsync(
        int recordId,
        CancellationToken ct = default)
    {
        try
        {
            var record = await _db.WorkoutRecords
                .Include(r => r.Exercise)
                .Include(r => r.WorkoutSets)
                .Include(r => r.Session)
                    .ThenInclude(s => s.Workout)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == recordId, ct);

            if (record is not null)
            {
                return record;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Помилка пошуку запису тренування {RecordId} в БД, перевіряємо локальну пам'ять",
                recordId);
        }

        return WorkoutRecordRepository.GetInMemoryById(recordId);
    }

    public async Task<bool> DeleteRecordAsync(int recordId, CancellationToken ct = default)
    {
        bool deletedInDb = false;

        try
        {
            var record = await _db.WorkoutRecords
                .Include(r => r.WorkoutSets)
                .FirstOrDefaultAsync(r => r.Id == recordId, ct);

            if (record is not null)
            {
                _db.WorkoutRecords.Remove(record);
                await _db.SaveChangesAsync(ct);
                deletedInDb = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Помилка видалення запису тренування {RecordId} з БД, видаляємо з локальної пам'яті",
                recordId);
        }

        bool deletedInMem = WorkoutRecordRepository.RemoveInMemory(recordId);
        return deletedInDb || deletedInMem;
    }
}
