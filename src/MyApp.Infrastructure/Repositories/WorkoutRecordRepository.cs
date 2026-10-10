using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public class WorkoutRecordRepository : IWorkoutRecordRepository
{
    private readonly AppDbContext _context;

    public WorkoutRecordRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(WorkoutRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _context.WorkoutRecords.AddAsync(record, ct);
        await _context.SaveChangesAsync(ct);
    }
}
