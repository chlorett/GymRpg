using Microsoft.EntityFrameworkCore;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public class UserProgressRepository : IUserProgressRepository
{
    private readonly AppDbContext _context;

    public UserProgressRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<UserProgress?> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return await _context.UserProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
    }

    public async Task SaveAsync(UserProgress progress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        if (progress.Id == 0)
        {
            await _context.UserProgresses.AddAsync(progress, ct);
        }
        else
        {
            _context.UserProgresses.Update(progress);
        }

        await _context.SaveChangesAsync(ct);
    }
}
