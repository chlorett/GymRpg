using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class WorkoutSessionRepository : IWorkoutSessionRepository
{
    private const long FirstInMemId = 100_000;

    private static readonly object SyncRoot = new();

    private static readonly List<WorkoutSession> InMemSessions = new();

    private readonly AppDbContext _context;
    private readonly ILogger<WorkoutSessionRepository> _logger;

    public WorkoutSessionRepository(AppDbContext context, ILogger<WorkoutSessionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WorkoutSession>> GetSessionsByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading workout sessions for user {UserId}", userId);

        try
        {
            return await _context.WorkoutSessions
                .AsNoTracking()
                .Include(s => s.Workout)
                    .ThenInclude(w => w.WorkoutExercises)
                .Where(s => s.Workout.UserId == userId)
                .OrderBy(s => s.ScheduledDate)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Помилка завантаження сесій користувача {UserId} з БД, використовуємо локальний список",
                userId);
        }

        lock (SyncRoot)
        {
            return InMemSessions
                .Where(s => s.Workout.UserId == userId)
                .OrderBy(s => s.ScheduledDate)
                .ToList();
        }
    }

    public async Task<WorkoutSession?> GetSessionByIdAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.WorkoutSessions
                .AsNoTracking()
                .Include(s => s.Workout)
                .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Помилка завантаження сесії {SessionId} з БД, перевіряємо локальний список",
                sessionId);
        }

        lock (SyncRoot)
        {
            return InMemSessions.Find(s => s.Id == sessionId);
        }
    }

    public async Task AddWorkoutWithSessionAsync(
        Workout workout,
        WorkoutSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            session.Workout = workout;
            _context.Workouts.Add(workout);
            _context.WorkoutSessions.Add(session);

            // Один SaveChanges = одна транзакція: воркаут і сесія разом або нічого.
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Помилка збереження сесії в БД, зберігаємо в локальний список");

            // Прибираємо невдалі сутності з контексту, щоб наступний SaveChanges не повторював їх.
            _context.ChangeTracker.Clear();
        }

        lock (SyncRoot)
        {
            long id = InMemSessions.Count == 0 ? FirstInMemId : InMemSessions.Max(s => s.Id) + 1;

            workout.Id = id;
            int exerciseIndex = 0;
            foreach (var exercise in workout.WorkoutExercises)
            {
                exerciseIndex++;
                exercise.Id = (id * 100) + exerciseIndex;
                exercise.WorkoutId = id;
            }

            session.Id = id;
            session.WorkoutId = id;
            session.Workout = workout;
            workout.WorkoutSessions.Add(session);

            InMemSessions.Add(session);
        }
    }

    public async Task<bool> UpdateSessionStatusAsync(
        int sessionId,
        SessionStatus status,
        DateTimeOffset? completedAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _context.WorkoutSessions
                .Where(s => s.Id == sessionId)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(s => s.Status, status)
                        .SetProperty(s => s.CompletedAt, completedAt),
                    cancellationToken);

            return rows > 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Помилка оновлення статусу сесії {SessionId} в БД, оновлюємо локальний список",
                sessionId);
        }

        lock (SyncRoot)
        {
            var inMem = InMemSessions.Find(s => s.Id == sessionId);
            if (inMem is null)
            {
                return false;
            }

            inMem.Status = status;
            inMem.CompletedAt = completedAt;
            return true;
        }
    }
}