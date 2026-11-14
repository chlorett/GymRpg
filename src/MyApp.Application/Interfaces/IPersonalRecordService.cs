using MyApp.Application.Dtos;

namespace MyApp.Application.Interfaces;

public interface IPersonalRecordService
{
    Task<PersonalRecordsOverviewDto> GetPersonalRecordsOverviewAsync(
        int userId,
        int? exerciseIdFilter = null,
        string? honestyFilter = null,
        CancellationToken ct = default);

    Task<DeleteWorkoutRecordResultDto> DeleteWorkoutRecordAsync(
        int userId,
        int recordId,
        CancellationToken ct = default);
}
