using System.Globalization;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public sealed class ExerciseCatalogService : IExerciseCatalogService
{
    private static readonly StringComparer NameComparer =
        StringComparer.Create(new CultureInfo("uk-UA"), ignoreCase: true);

    private readonly IExerciseRepository repository;
    private readonly ILogger<ExerciseCatalogService> logger;

    public ExerciseCatalogService(IExerciseRepository repository, ILogger<ExerciseCatalogService> logger)
    {
        this.repository = repository;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<ExerciseCatalogItemDto>> GetExercisesAsync(
        string? searchQuery,
        EquipmentType? equipmentType,
        CancellationToken ct = default)
    {
        var query = searchQuery?.Trim();

        this.logger.LogInformation(
            "Запит каталогу вправ: пошук '{SearchQuery}', обладнання {EquipmentType}",
            query,
            equipmentType);

        var exercises = await this.repository.GetAllAsync(ct).ConfigureAwait(false);

        var result = exercises
            .Where(e => !e.IsDeleted)
            .Where(e => equipmentType is null || e.EquipmentType == equipmentType)
            .Where(e => string.IsNullOrEmpty(query) || Matches(e, query))
            .OrderBy(e => e.Name, NameComparer)
            .Select(ToDto)
            .ToList();

        this.logger.LogInformation(
            "Результат фільтрації каталогу вправ: знайдено {Count}",
            result.Count);

        return result;
    }

    private static bool Matches(Exercise exercise, string query) =>
        exercise.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (exercise.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

    private static ExerciseCatalogItemDto ToDto(Exercise exercise) =>
        new(
            exercise.Id,
            exercise.Name,
            exercise.EquipmentType,
            GetEquipmentLabel(exercise.EquipmentType),
            exercise.Description ?? string.Empty,
            exercise.Technique ?? string.Empty);

    private static string GetEquipmentLabel(EquipmentType equipmentType) => equipmentType switch
    {
        EquipmentType.Gym => "Тренажерний зал",
        EquipmentType.Calisthenics => "Калістеніка / Власна вага",
        _ => equipmentType.ToString(),
    };
}