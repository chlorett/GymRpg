using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Application.Services;

public class WorkoutTrackingService : IWorkoutTrackingService
{
    private readonly IWorkoutRecordRepository _recordRepository;
    private readonly IUserProgressRepository _progressRepository;
    private readonly IUserRepository? _userRepository;
    private readonly IExerciseRepository? _exerciseRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WorkoutTrackingService> _logger;

    public WorkoutTrackingService(
        IWorkoutRecordRepository recordRepository,
        IUserProgressRepository progressRepository,
        TimeProvider timeProvider,
        ILogger<WorkoutTrackingService> logger,
        IUserRepository? userRepository = null,
        IExerciseRepository? exerciseRepository = null)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _progressRepository = progressRepository ?? throw new ArgumentNullException(nameof(progressRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userRepository = userRepository;
        _exerciseRepository = exerciseRepository;
    }

    public async Task<OperationResult> TrackAsync(TrackWorkoutRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            _logger.LogWarning("Спроба зберегти тренування з порожнім запитом");
            return OperationResult.Fail(ErrorCodes.ValidationFailed, null, "Запит не може бути порожнім.");
        }

        if (request.UserId <= 0)
        {
            _logger.LogWarning("Некоректний UserId: {UserId}", request.UserId);
            return OperationResult.Fail(ErrorCodes.ValidationFailed, nameof(request.UserId), "Некоректний ідентифікатор користувача.");
        }

        if (request.ExerciseId <= 0)
        {
            _logger.LogWarning("Не обрано вправу (ExerciseId: {ExerciseId}) для користувача {UserId}", request.ExerciseId, request.UserId);
            return OperationResult.Fail(ErrorCodes.ValidationFailed, nameof(request.ExerciseId), "Оберіть вправу.");
        }

        if (request.Sets is null || request.Sets.Count == 0)
        {
            _logger.LogWarning("Спроба зберегти тренування без підходів для користувача {UserId}", request.UserId);
            return OperationResult.Fail(ErrorCodes.ValidationFailed, nameof(request.Sets), "Тренування повинно містити щонайменше один підхід.");
        }

        foreach (var set in request.Sets)
        {
            if (set.Reps <= 0)
            {
                _logger.LogWarning("Некоректна кількість повторень ({Reps}) для користувача {UserId}", set.Reps, request.UserId);
                return OperationResult.Fail(ErrorCodes.ValidationFailed, nameof(set.Reps), "Кількість повторень повинна бути більшою за нуль.");
            }

            if (set.Reps > 500)
            {
                _logger.LogWarning("Занадто велика кількість повторень ({Reps}) для користувача {UserId}", set.Reps, request.UserId);
                return OperationResult.Fail(ErrorCodes.ValidationFailed, nameof(set.Reps), "Кількість повторень не може перевищувати 500.");
            }

            if (set.Weight < 0)
            {
                _logger.LogWarning("Від'ємна вага ({Weight}) для користувача {UserId}", set.Weight, request.UserId);
                return OperationResult.Fail(ErrorCodes.ValidationFailed, nameof(set.Weight), "Вага не може бути від'ємною.");
            }

            if (set.Weight > 1000)
            {
                _logger.LogWarning("Занадто велика вага ({Weight}) для користувача {UserId}", set.Weight, request.UserId);
                return OperationResult.Fail(ErrorCodes.ValidationFailed, nameof(set.Weight), "Вага не може перевищувати 1000 кг.");
            }
        }

        if (_userRepository is not null)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, ct);
            if (user is null)
            {
                _logger.LogWarning("Користувача з Id {UserId} не знайдено при спробі зберегти тренування", request.UserId);
                return OperationResult.Fail(ErrorCodes.UserNotFound, nameof(request.UserId), "Користувача не знайдено.");
            }
        }

        string exerciseName = $"Вправа #{request.ExerciseId}";
        if (_exerciseRepository is not null)
        {
            var exercise = await _exerciseRepository.GetByIdAsync(request.ExerciseId, ct);
            if (exercise is null)
            {
                _logger.LogWarning("Вправу з Id {ExerciseId} не знайдено для користувача {UserId}", request.ExerciseId, request.UserId);
                return OperationResult.Fail(ErrorCodes.ExerciseNotFound, nameof(request.ExerciseId), "Вправу не знайдено.");
            }

            exerciseName = exercise.Name;
        }

        int xpEarned = XpCalculator.CalculateXp(request.Sets);
        bool isSuspicious = XpCalculator.IsSuspicious(request.Sets);

        if (isSuspicious)
        {
            _logger.LogWarning(
                "Виявлено підозрілий запис тренування для користувача {UserId}, вправа {ExerciseName} (Id: {ExerciseId})",
                request.UserId,
                exerciseName,
                request.ExerciseId);
        }

        var progress = await _progressRepository.GetByUserIdAsync(request.UserId, ct);
        if (progress is null)
        {
            progress = new UserProgress
            {
                UserId = request.UserId,
                Xp = xpEarned,
                Level = XpCalculator.CalculateLevel(xpEarned),
            };
        }
        else
        {
            progress.Xp += xpEarned;
            progress.Level = XpCalculator.CalculateLevel(progress.Xp);
        }

        await _progressRepository.SaveAsync(progress, ct);

        var workoutRecord = new WorkoutRecord
        {
            UserId = request.UserId,
            ExerciseId = request.ExerciseId,
            CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            XpEarned = xpEarned,
            IsSuspicious = isSuspicious,
            Sets = request.Sets.Select(s => new WorkoutSet
            {
                Reps = s.Reps,
                Weight = s.Weight,
            }).ToList(),
        };

        await _recordRepository.AddAsync(workoutRecord, ct);

        _logger.LogInformation(
            "Успішно збережено тренування для користувача {UserId}: вправа '{ExerciseName}', підходів: {SetsCount}, зароблено {XpEarned} XP, поточний рівень: {Level}",
            request.UserId,
            exerciseName,
            request.Sets.Count,
            xpEarned,
            progress.Level);

        return OperationResult.Success();
    }
}
