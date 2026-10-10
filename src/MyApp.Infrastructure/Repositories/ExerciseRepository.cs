using Microsoft.EntityFrameworkCore;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public class ExerciseRepository : IExerciseRepository
{
    private readonly AppDbContext _context;

    public ExerciseRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Exercise?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Exercises.FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyList<Exercise>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Exercises
            .OrderBy(e => e.Name)
            .ToListAsync(ct);
    }
}
