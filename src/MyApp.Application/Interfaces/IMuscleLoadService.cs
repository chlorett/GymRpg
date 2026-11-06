using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IMuscleLoadService
{
    Task<MuscleLoadOverviewDto> GetUserMuscleLoadOverviewAsync(
        int userId,
        string? statusFilter = null,
        CancellationToken ct = default);

    Task<RecalculateMuscleLoadResultDto> RecalculateMuscleLoadAsync(
        int userId,
        CancellationToken ct = default);
}
