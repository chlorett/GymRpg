using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IWorkoutTrackingService
{
    Task<OperationResult> TrackAsync(TrackWorkoutRequest request, CancellationToken ct = default);
}
