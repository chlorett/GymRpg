using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Interfaces;

public interface IWorkoutSessionRepository
{
    Task<IReadOnlyList<WorkoutSession>> GetSessionsByUserIdAsync(
        int userId, CancellationToken cancellationToken = default);

    Task<WorkoutSession?> GetSessionByIdAsync(
        int sessionId, CancellationToken cancellationToken = default);

    Task AddWorkoutWithSessionAsync(
        Workout workout, WorkoutSession session, CancellationToken cancellationToken = default);

    Task<bool> UpdateSessionStatusAsync(
        int sessionId, SessionStatus status, DateTimeOffset? completedAt, CancellationToken cancellationToken = default);
}