using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IPersonalRecordService
{
    Task<OperationResult<PersonalRecordsOverviewDto>> GetPersonalRecordsOverviewAsync(
        int userId,
        int? exerciseIdFilter = null,
        string? honestyFilter = null,
        CancellationToken ct = default);

    Task<OperationResult<DeleteWorkoutRecordResultDto>> DeleteWorkoutRecordAsync(
        int userId,
        int recordId,
        CancellationToken ct = default);
}
