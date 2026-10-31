using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IWorkoutTemplateRepository
{
    // Чи є в цього тренера шаблон із такою назвою (без урахування регістру).
    Task<bool> ExistsByNameAsync(int trainerId, string name, CancellationToken ct = default);

    Task AddAsync(WorkoutTemplate template, CancellationToken ct = default);

    Task<IReadOnlyList<WorkoutTemplate>> GetByUserIdAsync(int userId, CancellationToken ct = default);

    Task<WorkoutTemplate?> GetByIdWithExercisesAsync(int templateId, CancellationToken ct = default);

    Task DeleteAsync(int templateId, CancellationToken ct = default);
}
