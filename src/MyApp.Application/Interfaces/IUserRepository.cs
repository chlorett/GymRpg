using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyApp.Application.Dtos;
using MyApp.Domain.Entities;

namespace MyApp.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id, CancellationToken ct = default);

        Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

        Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);

        Task AddAsync(User user, CancellationToken ct = default);

        Task<bool> UpdateProfileAsync(
            int userId,
            decimal? weightKg,
            decimal? heightCm,
            DateOnly? birthDate,
            CancellationToken ct = default);

        Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken ct = default);

        Task<IReadOnlyList<Region>> GetRegionsAsync(CancellationToken ct = default);

        Task<IReadOnlyList<Gym>> GetGymsAsync(CancellationToken ct = default);

        Task<IReadOnlyList<Division>> GetDivisionsAsync(CancellationToken ct = default);

        Task<int> CountByGymAsync(int gymId, CancellationToken ct = default);

        Task<bool> UpdateAffiliationAsync(
            int userId,
            string username,
            int? gymId,
            int? divisionId,
            CancellationToken ct = default);

        Task<IReadOnlyList<GymRosterMemberDto>> GetGymRosterAsync(int? gymId, int? divisionId);

        Task<bool> UpdateModerationStatusAsync(
            int userId,
            bool isExcludedFromLeaderboard,
            bool isBlocked,
            DateTimeOffset? blockedAt);
    }
}