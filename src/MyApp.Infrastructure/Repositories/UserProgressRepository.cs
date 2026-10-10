using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class UserProgressRepository : IUserProgressRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserProgressRepository> _logger;

    public UserProgressRepository(AppDbContext db, ILogger<UserProgressRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UserProgress?> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Loading progress for user {UserId}", userId);

        return await _db.Set<UserProgress>()
            .AsNoTracking()
            .FirstOrDefaultAsync(progress => progress.UserId == userId, ct);
    }

    public async Task SaveAsync(UserProgress progress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        _logger.LogDebug("Saving progress for user {UserId}", progress.UserId);

        var exists = await _db.Set<UserProgress>().AnyAsync(item => item.UserId == progress.UserId, ct);
        if (exists)
        {
            _db.Set<UserProgress>().Update(progress);
        }
        else
        {
            _db.Set<UserProgress>().Add(progress);
        }

        await _db.SaveChangesAsync(ct);
    }
}
