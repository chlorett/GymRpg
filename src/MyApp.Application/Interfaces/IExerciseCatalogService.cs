using MyApp.Application.Dtos;
using MyApp.Domain.Enums;

namespace MyApp.Application.Interfaces;

public interface IExerciseCatalogService
{
    Task<OperationResult<IReadOnlyList<ExerciseCatalogItemDto>>> GetExercisesAsync(
        string? searchQuery,
        EquipmentType? equipmentType,
        CancellationToken ct = default);

    Task<OperationResult> SaveExerciseAsync(SaveExerciseRequest request, UserRole actorRole, CancellationToken ct = default);

    Task<OperationResult> ArchiveExerciseAsync(int exerciseId, UserRole actorRole, CancellationToken ct = default);
}