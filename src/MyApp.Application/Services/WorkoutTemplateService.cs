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
}
