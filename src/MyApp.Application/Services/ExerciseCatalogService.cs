using System.Globalization;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public sealed class ExerciseCatalogService : IExerciseCatalogService
{
    private const int MaxSearchQueryLength = 100;
    private const int MinNameLength = 3;
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 500;
    private const int MaxTechniqueLength = 2000;

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

        if (query is { Length: > MaxSearchQueryLength })
        {
            this.logger.LogWarning(
                "Помилка валідації пошукового запиту каталогу вправ [{ErrorCode}]: довжина запиту ({Length}) перевищує ліміт {MaxLength}",
                ErrorCodes.ValidationFailed,
                query.Length,
                MaxSearchQueryLength);
            return Array.Empty<ExerciseCatalogItemDto>();
        }

        if (equipmentType is { } eq && !Enum.IsDefined(eq))
        {
            this.logger.LogWarning(
                "Помилка валідації фільтра каталогу вправ [{ErrorCode}]: невідомий тип обладнання {EquipmentType}",
                ErrorCodes.ValidationFailed,
                eq);
            return Array.Empty<ExerciseCatalogItemDto>();
        }

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

    public async Task<OperationResult> SaveExerciseAsync(
       SaveExerciseRequest request,
       UserRole actorRole,
       CancellationToken ct = default)
    {
        if (!IsManager(actorRole))
        {
            return this.Fail(ErrorCodes.NotATrainer, "Керувати каталогом вправ можуть лише тренер або адміністратор.");
        }

        var name = request.Name?.Trim() ?? string.Empty;
        var description = request.Description?.Trim();
        var technique = request.Technique?.Trim();

        if (name.Length < MinNameLength || name.Length > MaxNameLength)
        {
            return this.Fail(
                ErrorCodes.ValidationFailed,
                $"Назва вправи має містити від {MinNameLength} до {MaxNameLength} символів.",
                nameof(SaveExerciseRequest.Name));
        }

        if (!Enum.IsDefined(request.EquipmentType))
        {
            return this.Fail(
                ErrorCodes.ValidationFailed,
                "Невідомий тип обладнання.",
                nameof(SaveExerciseRequest.EquipmentType));
        }

        if (string.IsNullOrWhiteSpace(technique) || technique.Length > MaxTechniqueLength)
        {
            return this.Fail(
                ErrorCodes.ValidationFailed,
                $"Техніка виконання обов'язкова (до {MaxTechniqueLength} символів).",
                nameof(SaveExerciseRequest.Technique));
        }

        if (description is { Length: > MaxDescriptionLength })
        {
            return this.Fail(
                ErrorCodes.ValidationFailed,
                $"Опис не може перевищувати {MaxDescriptionLength} символів.",
                nameof(SaveExerciseRequest.Description));
        }

        if (request.Id is { } id)
        {
            return await this.UpdateExerciseAsync(id, description, technique, ct).ConfigureAwait(false);
        }

        var exercises = await this.repository.GetAllAsync(ct).ConfigureAwait(false);
        if (exercises.Any(e => !e.IsDeleted && NameComparer.Equals(e.Name, name)))
        {
            return this.Fail(
                ErrorCodes.ExerciseNameTaken,
                "Вправа з такою назвою вже існує в каталозі.",
                nameof(SaveExerciseRequest.Name));
        }

        var created = await this.repository.AddAsync(
            new Exercise
            {
                Name = name,
                EquipmentType = request.EquipmentType,
                Description = description,
                Technique = technique,
            },
            ct).ConfigureAwait(false);

        this.logger.LogInformation("Вправу «{Name}» додано до каталогу (Id {ExerciseId})", created.Name, created.Id);
        return OperationResult.Success();
    }

    public async Task<OperationResult> ArchiveExerciseAsync(
        int exerciseId,
        UserRole actorRole,
        CancellationToken ct = default)
    {
        if (!IsManager(actorRole))
        {
            return this.Fail(ErrorCodes.NotATrainer, "Архівувати вправи можуть лише тренер або адміністратор.");
        }

        var archived = await this.repository.SoftDeleteAsync(exerciseId, ct).ConfigureAwait(false);
        if (!archived)
        {
            return this.Fail(ErrorCodes.ExerciseNotFound, "Вправу не знайдено або вона вже архівована.");
        }

        this.logger.LogInformation("Вправу {ExerciseId} архівовано (soft delete)", exerciseId);
        return OperationResult.Success();
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
        EquipmentType.Other => "Інше",
        _ => equipmentType.ToString(),
    };

    private static bool IsManager(UserRole role) => role is UserRole.Trainer or UserRole.Admin;

    private async Task<OperationResult> UpdateExerciseAsync(
        int id,
        string? description,
        string? technique,
        CancellationToken ct)
    {
        var updated = await this.repository.UpdateAsync(id, description, technique, ct).ConfigureAwait(false);
        if (!updated)
        {
            return this.Fail(ErrorCodes.ExerciseNotFound, "Вправу не знайдено або вона вже архівована.");
        }

        this.logger.LogInformation("Опис і техніку вправи {ExerciseId} оновлено", id);
        return OperationResult.Success();
    }

    private OperationResult Fail(string errorCode, string message, string? field = null)
    {
        this.logger.LogWarning(
            "Помилка керування каталогом вправ [{ErrorCode}]: {Message} (поле: {Field})",
            errorCode,
            message,
            field);
        return OperationResult.Fail(errorCode: errorCode, message: message, field: field);
    }
}
