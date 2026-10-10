using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IExerciseRepository
{
    Task<Exercise?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Exercise>> GetAllAsync(CancellationToken ct = default);
}
