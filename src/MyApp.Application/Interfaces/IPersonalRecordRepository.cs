using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IPersonalRecordRepository
{
    Task<IReadOnlyList<WorkoutRecord>> GetAllByUserIdAsync(int userId, CancellationToken ct = default);

    Task<WorkoutRecord?> GetByIdWithDetailsAsync(int recordId, CancellationToken ct = default);

    Task<bool> DeleteRecordAsync(int recordId, CancellationToken ct = default);
}
