using MyApp.Application.Dtos;
using MyApp.Domain.Enums;

namespace MyApp.Application.Interfaces;

public interface IWorkoutSessionService
{
    Task<IReadOnlyList<WorkoutSessionItemDto>> GetUserSessionsAsync(
        int userId,
        SessionStatus? statusFilter,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ScheduleSessionFromTemplateAsync(
        ScheduleWorkoutSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateSessionStatusAsync(
        UpdateSessionStatusRequest request,
        CancellationToken cancellationToken = default);
}