using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IMuscleLoadRepository
{
    Task<IReadOnlyList<MuscleGroup>> GetAllMuscleGroupsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ExerciseMuscleGroup>> GetAllExerciseMuscleGroupsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MuscleLoad>> GetUserMuscleLoadsAsync(int userId, CancellationToken ct = default);

    Task SaveUserMuscleLoadsAsync(int userId, IReadOnlyList<MuscleLoad> loads, CancellationToken ct = default);
}
