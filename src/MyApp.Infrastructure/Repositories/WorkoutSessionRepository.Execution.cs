using Microsoft.EntityFrameworkCore;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Infrastructure.Repositories;

public sealed partial class WorkoutSessionRepository : IWorkoutSessionRepository
{
    public async Task<WorkoutSession?> GetSessionWithExercisesAsync(
        int sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkoutSessions
            .AsNoTracking()
            .Include(s => s.Workout)
                .ThenInclude(w => w.WorkoutExercises)
                    .ThenInclude(we => we.Exercise)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }

    public async Task<SessionCompletionOutcome?> CompleteSessionWithRecordsAsync(
        int sessionId,
        int userId,
        IReadOnlyList<WorkoutRecord> records,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Атомарний перехід Planned -> Completed: захист від подвійного завершення.
        int updated = await _context.WorkoutSessions
            .Where(s => s.Id == sessionId && s.Status == SessionStatus.Planned)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, SessionStatus.Completed)
                      .SetProperty(x => x.CompletedAt, completedAt),
                cancellationToken);

        if (updated == 0)
        {
            return null;
        }

        _context.WorkoutRecords.AddRange(records);

        int earnedXp = records.Sum(r => r.XpEarned);

        var progress = await _context.UserProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (progress is null)
        {
            progress = new UserProgress { UserId = userId };
            _context.UserProgresses.Add(progress);
        }

        progress.Xp += earnedXp;
        progress.Level = XpCalculator.CalculateLevel((int)Math.Min(progress.Xp, int.MaxValue));
        progress.UpdatedAt = completedAt;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SessionCompletionOutcome(progress.Xp, progress.Level);
    }
}