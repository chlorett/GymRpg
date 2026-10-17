using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IWorkoutRecordRepository
{
    Task AddAsync(WorkoutRecord record, CancellationToken ct = default);

    /// <summary>
    /// Повертає останні <paramref name="count"/> записів тренувань для заданого атлета.
    /// </summary>
    Task<IReadOnlyList<WorkoutRecord>> GetRecentByUserIdAsync(
        int userId,
        int count = 10,
        CancellationToken ct = default);
}
