using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IWorkoutTemplateService
{
    Task<OperationResult> CreateAsync(CreateTemplateRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<WorkoutTemplateSummaryDto>> GetUserTemplatesAsync(
        int userId,
        string? searchQuery = null,
        CancellationToken ct = default);

    Task<WorkoutTemplateDetailsDto?> GetTemplateDetailsAsync(
        int templateId,
        int userId,
        CancellationToken ct = default);

    Task<OperationResult> DeleteTemplateAsync(
        int templateId,
        int userId,
        CancellationToken ct = default);
}
