using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IWorkoutRecordRepository
{
    Task AddAsync(WorkoutRecord record, CancellationToken ct = default);
}
