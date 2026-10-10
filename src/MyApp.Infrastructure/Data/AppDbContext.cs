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

    public DbSet<Exercise> Exercises => Set<Exercise>();

    public DbSet<User> Users => Set<User>();

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

        modelBuilder.Entity<Exercise>(builder =>
        {
            builder.HasKey(e => e.Id);
            builder.HasData(
                new Exercise { Id = 1, Name = "Жим лежачи" },
                new Exercise { Id = 2, Name = "Присідання зі штангою" },
                new Exercise { Id = 3, Name = "Станова тяга" },
                new Exercise { Id = 4, Name = "Армійський жим" },
                new Exercise { Id = 5, Name = "Підтягування" },
                new Exercise { Id = 6, Name = "Віджимання на брусах" });
        });

        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(u => u.Id);
            builder.HasData(
                new User
                {
                    Id = 1,
                    Username = "athlete",
                    Email = "athlete@pulseforge.app",
                    PasswordHash = "seed_hash",
                    WeightKg = 75m,
                    HeightCm = 180m,
                    BirthDate = new DateOnly(2000, 1, 1),
                });
        });
    }
}
