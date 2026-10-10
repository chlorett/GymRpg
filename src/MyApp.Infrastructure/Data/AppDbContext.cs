using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkoutRecord> WorkoutRecords => Set<WorkoutRecord>();

    public DbSet<WorkoutSet> WorkoutSets => Set<WorkoutSet>();

    public DbSet<UserProgress> UserProgresses => Set<UserProgress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WorkoutRecord>(builder =>
        {
            builder.HasKey(r => r.Id);
            builder.HasMany(r => r.Sets)
                .WithOne()
                .HasForeignKey(s => s.WorkoutRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkoutSet>(builder =>
        {
            builder.HasKey(s => s.Id);
        });

        modelBuilder.Entity<UserProgress>(builder =>
        {
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => p.UserId).IsUnique();
        });
    }
}
