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

            await SeedExercisesAsync(db, logger);
            await SeedUsersAndProgressAsync(db, scope.ServiceProvider, logger);

            logger.LogInformation("Базу даних успішно ініціалізовано.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Не вдалося з'єднатися з PostgreSQL. Застосунок автоматично використовуватиме вбудований автономний режим.");
        }
    }

    private static async Task SeedExercisesAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Exercises.AnyAsync())
        {
            return;
        }

        logger.LogInformation("Засівання каталогу базових вправ...");
        db.Exercises.AddRange(
            new Exercise { Name = "Жим лежачи на лаві", Description = "Базова вправа для розвитку грудних м'язів, трицепсів та передньої дельти." },
            new Exercise { Name = "Присідання зі штангою", Description = "Основна вправа для розвитку квадрицепсів, сідниць та м'язів кора." },
            new Exercise { Name = "Станова тяга", Description = "Фундаментальна вправа для спини, біцепсів стегон та трапецій." },
            new Exercise { Name = "Підтягування на перекладині", Description = "Вправа з власною вагою для найширших м'язів спини та біцепсів." },
            new Exercise { Name = "Армійський жим", Description = "Жим штанги стоячи для розвитку плечового пояса та дельт." },
            new Exercise { Name = "Віджимання на брусах", Description = "Вправа для нижньої частини грудей та трицепсів." },
            new Exercise { Name = "Тяга штанги в нахилі", Description = "Вправа для товщини м'язів спини." },
            new Exercise { Name = "Згинання рук зі штангою", Description = "Ізолююча вправа для біцепсів." },
            new Exercise { Name = "Жим гантелей на похилій лаві", Description = "Вправа для верхньої частини грудей та передньої дельти." },
            new Exercise { Name = "Румунська тяга", Description = "Вправа для біцепсів стегон та сідниць." });
        await db.SaveChangesAsync();
    }

    private static async Task SeedUsersAndProgressAsync(
        AppDbContext db,
        IServiceProvider services,
        ILogger logger)
    {
        if (!await db.Users.AnyAsync())
        {
            logger.LogInformation("Засівання базових користувачів (Атлет і Тренер)...");
            var hasher = services.GetRequiredService<IPasswordHasher>();
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
                UpdatedAt = DateTimeOffset.UtcNow,
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
                UpdatedAt = DateTimeOffset.UtcNow,
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
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync();
            }
        }

        await SeedLeaderboardRivalsAsync(db, services);
    }

    private static async Task SeedLeaderboardRivalsAsync(AppDbContext db, IServiceProvider services)
    {
        var rivals = new (string Username, string Email, decimal Weight, decimal Height, int Level, int Xp)[]
        {
            ("iron_max", "max@gym.com", 88.0m, 184.0m, 3, 2450),
            ("power_oleh", "oleh@gym.com", 82.0m, 179.0m, 2, 1650),
            ("fit_maria", "maria@gym.com", 62.0m, 168.0m, 2, 1120),
            ("rookie_artem", "artem@gym.com", 70.0m, 176.0m, 1, 480),
        };

        string? cachedHash = null;
        foreach (var rival in rivals)
        {
            if (await db.Users.AnyAsync(u => u.Username == rival.Username))
            {
                continue;
            }

            cachedHash ??= services.GetRequiredService<IPasswordHasher>().Hash("123456");
            var user = new User
            {
                Email = rival.Email,
                Username = rival.Username,
                PasswordHash = cachedHash,
                Role = UserRole.User,
                WeightKg = rival.Weight,
                HeightCm = rival.Height,
                BirthDate = DateTime.UtcNow.AddYears(-24),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.UserProgresses.Add(new UserProgress
            {
                UserId = user.Id,
                Level = rival.Level,
                Xp = rival.Xp,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }
    }
}
