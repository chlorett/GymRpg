using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            logger.LogInformation("Ініціалізація бази даних PulseForge...");
            await db.Database.EnsureCreatedAsync();

            // 1. Початкові вправи (це змінила Софія)
            await ExerciseSeedData.SeedAsync(db, logger);

            // 2. Початкові користувачі (Атлет і Тренер)
            if (!await db.Users.AnyAsync())
            {
                logger.LogInformation("Засівання базових користувачів (Атлет і Тренер)...");
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                string defaultPasswordHash = hasher.Hash("123456");

                var athlete = new User
                {
                    Email = "athlete@gym.com",
                    Username = "athlete",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.User,
                    WeightKg = 75.0m,
                    HeightCm = 180.0m,
                    BirthDate = DateTime.UtcNow.AddYears(-22),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                var trainer = new User
                {
                    Email = "trainer@gym.com",
                    Username = "coach_ivan",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.Trainer,
                    WeightKg = 85.0m,
                    HeightCm = 185.0m,
                    BirthDate = DateTime.UtcNow.AddYears(-30),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                db.Users.AddRange(athlete, trainer);
                await db.SaveChangesAsync();

                if (!await db.UserProgresses.AnyAsync(p => p.UserId == athlete.Id))
                {
                    db.UserProgresses.Add(new UserProgress
                    {
                        UserId = athlete.Id,
                        Level = 1,
                        Xp = 0,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                    await db.SaveChangesAsync();
                }
            }

            logger.LogInformation("Базу даних успішно ініціалізовано.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Не вдалося з'єднатися з PostgreSQL. Застосунок автоматично використовуватиме вбудований автономний режим.");
        }
    }
}
