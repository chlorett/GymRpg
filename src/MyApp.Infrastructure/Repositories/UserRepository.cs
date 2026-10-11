using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(AppDbContext db, ILogger<UserRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _logger.LogDebug("Loading user {UserId}", id);

        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == id, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        _logger.LogDebug("Looking up user by email");

        var normalized = email.ToLowerInvariant();

        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email.ToLower() == normalized, ct);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(username);

        _logger.LogDebug("Looking up user by username {Username}", username);

        var normalized = username.ToLowerInvariant();

        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Username.ToLower() == normalized, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        _logger.LogDebug("Saving user {Username}", user.Username);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
    }
}
