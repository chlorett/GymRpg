using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces;

public interface IExerciseRepository
{
    Task<Exercise?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Exercise>> GetAllAsync(CancellationToken ct = default);

    Task<Exercise> AddAsync(Exercise exercise, CancellationToken ct = default);

    /// <summary>Оновлює опис і техніку активної вправи.</summary>
    /// <param name="id">Ідентифікатор вправи.</param>
    /// <param name="description">Новий опис.</param>
    /// <param name="technique">Нова техніка виконання.</param>
    /// <param name="ct">Токен скасування.</param>
    /// <returns><see langword="true"/>, якщо вправу оновлено; <see langword="false"/>, якщо її не знайдено.</returns>
    Task<bool> UpdateAsync(int id, string? description, string? technique, CancellationToken ct = default);

    /// <summary>Виконує м'яке видалення вправи.</summary>
    /// <param name="id">Ідентифікатор вправи.</param>
    /// <param name="ct">Токен скасування.</param>
    /// <returns><see langword="true"/>, якщо вправу архівовано; <see langword="false"/>, якщо її не знайдено або вона вже архівована.</returns>
    Task<bool> SoftDeleteAsync(int id, CancellationToken ct = default);
}