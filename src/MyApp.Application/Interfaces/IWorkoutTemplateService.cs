using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IWorkoutTemplateService
{
    Task<OperationResult> CreateAsync(CreateTemplateRequest request, CancellationToken ct = default);
}
