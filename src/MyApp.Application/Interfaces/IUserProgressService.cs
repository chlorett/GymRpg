using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

/// <summary>
/// Сервіс для отримання RPG-профілю та журналу тренувань атлета.
/// </summary>
public interface IUserProgressService
{
    /// <summary>
    /// Повертає повний RPG-профіль атлета разом із нещодавньою історією тренувань.
    /// Повертає <c>null</c>, якщо користувача не знайдено.
    /// </summary>
    Task<UserProgressDto?> GetProgressAsync(int userId, CancellationToken ct = default);
}
