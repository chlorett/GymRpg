using System.Globalization;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Rules;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public sealed class WorkoutTemplateService : IWorkoutTemplateService
{
    private readonly IWorkoutTemplateRepository _templates;
    private readonly IUserRepository _users;
    private readonly IExerciseRepository _exercises;
    private readonly TimeProvider _time;
    private readonly ILogger<WorkoutTemplateService> _logger;

    public WorkoutTemplateService(
            IWorkoutTemplateRepository templates,
            IUserRepository users,
            IExerciseRepository exercises,
            TimeProvider time,
            ILogger<WorkoutTemplateService> logger)
    {
        _templates = templates;
        _users = users;
        _exercises = exercises;
        _time = time;
        _logger = logger;
    }

    public static string GetEquipmentTypeLabel(EquipmentType? equipmentType) => equipmentType switch
    {
        EquipmentType.Gym => "Тренажерний зал",
        EquipmentType.Calisthenics => "Власна вага (Калістеніка)",
        _ => "Базовий інвентар",
    };

    public async Task<OperationResult> CreateAsync(CreateTemplateRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            _logger.LogWarning("Workout template rejected: request is null");
            return Fail(ErrorCodes.ValidationFailed, nameof(request), "Запит не може бути порожнім.");
        }

        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Reject(invalid, request);
        }

        var user = await _users.GetByIdAsync(request.TrainerId, ct);
        if (user is null)
        {
            var notFound = Fail(ErrorCodes.UserNotFound, nameof(CreateTemplateRequest.TrainerId), "Користувача не знайдено.");
            return Reject(notFound, request);
        }

        if (user.Role != UserRole.Trainer)
        {
            var notTrainer = Fail(ErrorCodes.NotATrainer, nameof(CreateTemplateRequest.TrainerId), "Створювати шаблони може лише тренер.");
            return Reject(notTrainer, request);
        }

        var name = request.Name.Trim();
        if (await _templates.ExistsByNameAsync(request.TrainerId, name, ct))
        {
            var taken = Fail(ErrorCodes.TemplateNameTaken, nameof(CreateTemplateRequest.Name), "Шаблон із такою назвою вже існує.");
            return Reject(taken, request);
        }

        foreach (var item in request.Exercises)
        {
            var exercise = await _exercises.GetByIdAsync(item.ExerciseId, ct);
            if (exercise is null || exercise.IsDeleted)
            {
                var notFound = Fail(ErrorCodes.ExerciseNotFound, nameof(CreateTemplateRequest.Exercises), "Вправу не знайдено.");
                return Reject(notFound, request);
            }
        }

        var template = CreateTemplate(request, name);
        await _templates.AddAsync(template, ct);

        _logger.LogInformation(
            "Workout template created: trainer {TrainerId}, name {Name}, exercises {ExerciseCount}",
            request.TrainerId,
            name,
            request.Exercises.Count);

        return OperationResult.Success();
    }

    public async Task<IReadOnlyList<WorkoutTemplateSummaryDto>> GetUserTemplatesAsync(
        int userId,
        string? searchQuery = null,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            _logger.LogWarning(
                "Workout templates requested with invalid userId: {UserId} [{ErrorCode}]",
                userId,
                ErrorCodes.ValidationFailed);
            return Array.Empty<WorkoutTemplateSummaryDto>();
        }

        _logger.LogDebug(
            "Loading workout templates for user {UserId} with searchQuery '{SearchQuery}'",
            userId,
            searchQuery ?? string.Empty);

        var templates = await _templates.GetByUserIdAsync(userId, ct);
        var summaries = templates
            .OrderByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapToSummaryDto)
            .ToList();

        var filtered = ApplySearchFilter(summaries, searchQuery);

        _logger.LogInformation(
            "Workout templates loaded for user {UserId}: {TotalCount} total, {ShownCount} shown",
            userId,
            summaries.Count,
            filtered.Count);

        return filtered;
    }

    public async Task<WorkoutTemplateDetailsDto?> GetTemplateDetailsAsync(
        int templateId,
        int userId,
        CancellationToken ct = default)
    {
        if (templateId <= 0 || userId <= 0)
        {
            _logger.LogWarning(
                "Template details rejected: invalid templateId={TemplateId} or userId={UserId} [{ErrorCode}]",
                templateId,
                userId,
                ErrorCodes.ValidationFailed);
            return null;
        }

        var template = await _templates.GetByIdWithExercisesAsync(templateId, ct);
        if (template is null)
        {
            _logger.LogWarning(
                "Template details not found: templateId={TemplateId} for userId={UserId} [{ErrorCode}]",
                templateId,
                userId,
                ErrorCodes.TemplateNotFound);
            return null;
        }

        if (template.TrainerId != userId)
        {
            _logger.LogWarning(
                "Template details access denied: templateId={TemplateId} belongs to {OwnerId}, requested by {UserId} [{ErrorCode}]",
                templateId,
                template.TrainerId,
                userId,
                ErrorCodes.TemplateAccessDenied);
            return null;
        }

        var orderedExercises = template.WorkoutTemplateExercises
            .OrderBy(e => e.Position)
            .ToList();

        var exerciseItems = new List<TemplateExerciseItemDto>(orderedExercises.Count);
        foreach (var item in orderedExercises)
        {
            var exercise = item.Exercise ?? await _exercises.GetByIdAsync(item.ExerciseId, ct);
            string exerciseName = exercise?.Name ?? $"Вправа #{item.ExerciseId}";
            string equipmentText = GetEquipmentTypeLabel(exercise?.EquipmentType);
            string schemeText = $"{item.TargetSets} × {item.TargetReps}";

            exerciseItems.Add(new TemplateExerciseItemDto(
                Position: item.Position,
                ExerciseId: item.ExerciseId,
                ExerciseName: exerciseName,
                EquipmentTypeText: equipmentText,
                TargetSets: item.TargetSets,
                TargetReps: item.TargetReps,
                SchemeText: schemeText));
        }

        int totalSets = exerciseItems.Sum(e => e.TargetSets);
        int totalVolumeReps = exerciseItems.Sum(e => e.TargetSets * e.TargetReps);
        string description = string.IsNullOrWhiteSpace(template.Description)
            ? $"Тренувальна програма ({exerciseItems.Count} вправ, {totalSets} підходів)"
            : template.Description;

        _logger.LogInformation(
            "Workout template details loaded: templateId={TemplateId}, exercises={ExerciseCount}",
            templateId,
            exerciseItems.Count);

        return new WorkoutTemplateDetailsDto(
            TemplateId: (int)template.Id,
            TrainerId: (int)template.TrainerId,
            Name: template.Name,
            Description: description,
            ExercisesCount: exerciseItems.Count,
            TotalSetsCount: totalSets,
            TotalVolumeReps: totalVolumeReps,
            CreatedAt: template.CreatedAt,
            CreatedAtText: template.CreatedAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
            Exercises: exerciseItems);
    }

    public async Task<OperationResult> DeleteTemplateAsync(
        int templateId,
        int userId,
        CancellationToken ct = default)
    {
        if (templateId <= 0)
        {
            var invalidTemplate = Fail(
                ErrorCodes.ValidationFailed,
                nameof(templateId),
                "Оберіть шаблон тренування для видалення.");
            return RejectDelete(invalidTemplate, templateId, userId);
        }

        if (userId <= 0)
        {
            var invalidUser = Fail(
                ErrorCodes.ValidationFailed,
                nameof(userId),
                "Некоректний користувач.");
            return RejectDelete(invalidUser, templateId, userId);
        }

        var template = await _templates.GetByIdWithExercisesAsync(templateId, ct);
        if (template is null)
        {
            var notFound = Fail(
                ErrorCodes.TemplateNotFound,
                nameof(templateId),
                "Шаблон тренування не знайдено.");
            return RejectDelete(notFound, templateId, userId);
        }

        if (template.TrainerId != userId)
        {
            var accessDenied = Fail(
                ErrorCodes.TemplateAccessDenied,
                nameof(userId),
                "Ви можете видаляти лише власні шаблони тренувань.");
            return RejectDelete(accessDenied, templateId, userId);
        }

        await _templates.DeleteAsync(templateId, ct);

        _logger.LogInformation(
            "Workout template deleted: templateId={TemplateId}, name={Name}, trainerId={TrainerId}",
            templateId,
            template.Name,
            userId);

        return OperationResult.Success();
    }

    private static WorkoutTemplateSummaryDto MapToSummaryDto(WorkoutTemplate template)
    {
        var exercises = template.WorkoutTemplateExercises ?? Array.Empty<WorkoutTemplateExercise>();
        int exercisesCount = exercises.Count;
        int totalSets = exercises.Sum(e => (int)e.TargetSets);
        int totalVolumeReps = exercises.Sum(e => e.TargetSets * e.TargetReps);
        string description = string.IsNullOrWhiteSpace(template.Description)
            ? $"Вправ: {exercisesCount} • Підходів: {totalSets}"
            : template.Description;

        return new WorkoutTemplateSummaryDto(
            TemplateId: (int)template.Id,
            TrainerId: (int)template.TrainerId,
            Name: template.Name,
            Description: description,
            ExercisesCount: exercisesCount,
            TotalSetsCount: totalSets,
            TotalVolumeReps: totalVolumeReps,
            CreatedAt: template.CreatedAt,
            CreatedAtText: template.CreatedAt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
    }

    private static IReadOnlyList<WorkoutTemplateSummaryDto> ApplySearchFilter(
        List<WorkoutTemplateSummaryDto> templates,
        string? searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return templates;
        }

        string normalized = searchQuery.Trim();
        return templates
            .Where(t =>
                t.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static OperationResult? Validate(CreateTemplateRequest request)
    {
        if (request.TrainerId <= 0)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(CreateTemplateRequest.TrainerId), "Некоректний користувач.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(CreateTemplateRequest.Name), "Введіть назву шаблону.");
        }

        if (request.Name.Trim().Length > TemplateRules.MaxNameLength)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(CreateTemplateRequest.Name),
                $"Назва не може бути довшою за {TemplateRules.MaxNameLength} символів.");
        }

        if (request.Exercises is null || request.Exercises.Count == 0)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(CreateTemplateRequest.Exercises), "Додайте хоча б одну вправу.");
        }

        for (var index = 0; index < request.Exercises.Count; index++)
        {
            var error = ValidateExercise(request.Exercises[index], index + 1);
            if (error is not null)
            {
                return error;
            }
        }

        var hasDuplicates = request.Exercises
            .GroupBy(e => e.ExerciseId)
            .Any(g => g.Count() > 1);
        if (hasDuplicates)
        {
            return Fail(ErrorCodes.ValidationFailed, nameof(CreateTemplateRequest.Exercises), "Вправа не може повторюватися в шаблоні.");
        }

        return null;
    }

    private static OperationResult? ValidateExercise(TemplateExerciseDto item, int number)
    {
        if (item.ExerciseId <= 0)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(TemplateExerciseDto.ExerciseId),
                $"Вправа №{number}: оберіть вправу.");
        }

        if (item.TargetSets is < TemplateRules.MinTargetSets or > WorkoutRules.MaxSets)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(TemplateExerciseDto.TargetSets),
                $"Вправа №{number}: підходів має бути від {TemplateRules.MinTargetSets} до {WorkoutRules.MaxSets}.");
        }

        if (item.TargetReps is < WorkoutRules.MinReps or > WorkoutRules.MaxReps)
        {
            return Fail(
                ErrorCodes.ValidationFailed,
                nameof(TemplateExerciseDto.TargetReps),
                $"Вправа №{number}: повторень має бути від {WorkoutRules.MinReps} до {WorkoutRules.MaxReps}.");
        }

        return null;
    }

    private static OperationResult Fail(string errorCode, string field, string message)
    {
        return OperationResult.Fail(errorCode, field, message);
    }

    private static WorkoutTemplateExercise ToTemplateExercise(TemplateExerciseDto item, int index)
    {
        return new WorkoutTemplateExercise
        {
            ExerciseId = item.ExerciseId,
            Position = (short)(index + 1),
            TargetSets = (short)item.TargetSets,
            TargetReps = (short)item.TargetReps,
        };
    }

    private WorkoutTemplate CreateTemplate(CreateTemplateRequest request, string name)
    {
        var now = _time.GetUtcNow();

        return new WorkoutTemplate
        {
            TrainerId = request.TrainerId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now,
            WorkoutTemplateExercises = request.Exercises.Select(ToTemplateExercise).ToList(),
        };
    }

    private OperationResult Reject(OperationResult result, CreateTemplateRequest request)
    {
        _logger.LogWarning(
            "Workout template rejected: trainer {TrainerId}, error {ErrorCode}, field {Field}",
            request.TrainerId,
            result.ErrorCode,
            result.Field);

        return result;
    }

    private OperationResult RejectDelete(OperationResult result, int templateId, int userId)
    {
        _logger.LogWarning(
            "Workout template deletion rejected: template {TemplateId}, user {UserId}, error {ErrorCode}, field {Field}",
            templateId,
            userId,
            result.ErrorCode,
            result.Field);

        return result;
    }
}
