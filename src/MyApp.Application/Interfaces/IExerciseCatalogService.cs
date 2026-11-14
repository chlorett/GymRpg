using MyApp.Application.Dtos;
using MyApp.Domain.Enums;

namespace MyApp.Application.Interfaces;

public interface IExerciseCatalogService
{
    Task<IReadOnlyList<ExerciseCatalogItemDto>> GetExercisesAsync(
        string? searchQuery,
        EquipmentType? equipmentType,
        CancellationToken ct = default);

    /// <summary>Створює нову вправу або оновлює опис і техніку наявної.</summary>
    /// <param name="request">Дані форми вправи.</param>
    /// <param name="actorRole">Роль користувача, який виконує операцію.</param>
    /// <param name="ct">Токен скасування.</param>
    /// <returns>Результат операції з кодом помилки та полем, якщо валідація не пройдена.</returns>
    Task<OperationResult> SaveExerciseAsync(SaveExerciseRequest request, UserRole actorRole, CancellationToken ct = default);

    /// <summary>Архівує вправу (м'яке видалення).</summary>
    /// <param name="exerciseId">Ідентифікатор вправи.</param>
    /// <param name="actorRole">Роль користувача, який виконує операцію.</param>
    /// <param name="ct">Токен скасування.</param>
    /// <returns>Результат операції.</returns>
    Task<OperationResult> ArchiveExerciseAsync(int exerciseId, UserRole actorRole, CancellationToken ct = default);
}