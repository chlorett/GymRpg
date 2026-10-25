using MyApp.Application.Dtos;
using MyApp.Domain.Enums;

namespace MyApp.Application.Interfaces;

public interface IExerciseCatalogService
{
    Task<IReadOnlyList<ExerciseCatalogItemDto>> GetExercisesAsync(
        string? searchQuery,
        EquipmentType? equipmentType,
        CancellationToken ct = default);
}