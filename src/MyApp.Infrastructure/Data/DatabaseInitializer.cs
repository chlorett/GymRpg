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

            // 1. Початкові вправи
            if (!await db.Exercises.AnyAsync())
            {
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
