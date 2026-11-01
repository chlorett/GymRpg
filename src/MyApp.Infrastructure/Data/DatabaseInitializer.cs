using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using MyApp.Infrastructure.Repositories;
using Npgsql;

namespace MyApp.Infrastructure.Data;

public static class DatabaseInitializer
{
    private const string SchemaAndTablesSql = """
        CREATE SCHEMA IF NOT EXISTS pulseforge;

        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE t.typname = 'equipment_type' AND n.nspname = 'pulseforge') THEN
                CREATE TYPE pulseforge.equipment_type AS ENUM ('gym', 'calisthenics', 'other');
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE t.typname = 'medal_condition_type' AND n.nspname = 'pulseforge') THEN
                CREATE TYPE pulseforge.medal_condition_type AS ENUM ('workouts_count', 'level', 'total_xp', 'streak_days');
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE t.typname = 'session_status' AND n.nspname = 'pulseforge') THEN
                CREATE TYPE pulseforge.session_status AS ENUM ('planned', 'completed', 'skipped', 'canceled');
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE t.typname = 'user_role' AND n.nspname = 'pulseforge') THEN
                CREATE TYPE pulseforge.user_role AS ENUM ('user', 'trainer', 'admin');
            END IF;
        END$$;

        CREATE TABLE IF NOT EXISTS pulseforge.divisions (
            id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            name VARCHAR(100) NOT NULL
        );

        CREATE TABLE IF NOT EXISTS pulseforge.regions (
            id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            name VARCHAR(100) NOT NULL
        );

        CREATE TABLE IF NOT EXISTS pulseforge.gyms (
            id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            name VARCHAR(150) NOT NULL,
            region_id INT NOT NULL REFERENCES pulseforge.regions(id) ON DELETE RESTRICT
        );

        CREATE TABLE IF NOT EXISTS pulseforge.exercises (
            id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            name VARCHAR(150) NOT NULL,
            description TEXT,
            technique TEXT,
            equipment_type pulseforge.equipment_type NOT NULL DEFAULT 'gym'::pulseforge.equipment_type,
            is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
            deleted_at TIMESTAMPTZ,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS pulseforge.muscle_groups (
            id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            name VARCHAR(60) NOT NULL
        );

        CREATE TABLE IF NOT EXISTS pulseforge.exercise_muscle_groups (
            exercise_id INT NOT NULL REFERENCES pulseforge.exercises(id) ON DELETE CASCADE,
            muscle_group_id INT NOT NULL REFERENCES pulseforge.muscle_groups(id) ON DELETE RESTRICT,
            load_factor NUMERIC(4,3) NOT NULL DEFAULT 1.0,
            PRIMARY KEY (exercise_id, muscle_group_id)
        );

        CREATE TABLE IF NOT EXISTS pulseforge.medals (
            id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            name VARCHAR(100) NOT NULL,
            description TEXT NOT NULL DEFAULT ''::text,
            condition_type pulseforge.medal_condition_type NOT NULL,
            condition_value INT NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS pulseforge.users (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            email VARCHAR(254) NOT NULL UNIQUE,
            username VARCHAR(30) NOT NULL UNIQUE,
            password_hash VARCHAR(255) NOT NULL,
            role pulseforge.user_role NOT NULL DEFAULT 'user'::pulseforge.user_role,
            weight_kg NUMERIC(5,2),
            height_cm NUMERIC(5,1),
            birth_date TIMESTAMPTZ,
            gym_id INT REFERENCES pulseforge.gyms(id) ON DELETE RESTRICT,
            division_id INT REFERENCES pulseforge.divisions(id) ON DELETE RESTRICT,
            is_blocked BOOLEAN NOT NULL DEFAULT FALSE,
            blocked_at TIMESTAMPTZ,
            is_excluded_from_leaderboard BOOLEAN NOT NULL DEFAULT FALSE,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS pulseforge.muscle_load (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            user_id BIGINT NOT NULL REFERENCES pulseforge.users(id) ON DELETE CASCADE,
            muscle_group_id INT NOT NULL REFERENCES pulseforge.muscle_groups(id) ON DELETE CASCADE,
            load_value NUMERIC(10,2) NOT NULL DEFAULT 0,
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE (user_id, muscle_group_id)
        );

        CREATE TABLE IF NOT EXISTS pulseforge.user_medals (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            user_id BIGINT NOT NULL REFERENCES pulseforge.users(id) ON DELETE CASCADE,
            medal_id INT NOT NULL REFERENCES pulseforge.medals(id) ON DELETE CASCADE,
            earned_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE (user_id, medal_id)
        );

        CREATE TABLE IF NOT EXISTS pulseforge.user_progress (
            user_id BIGINT PRIMARY KEY REFERENCES pulseforge.users(id) ON DELETE CASCADE,
            level INT NOT NULL DEFAULT 1,
            xp BIGINT NOT NULL DEFAULT 0,
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS pulseforge.workout_templates (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            trainer_id BIGINT NOT NULL REFERENCES pulseforge.users(id) ON DELETE CASCADE,
            trainer_role pulseforge.user_role NOT NULL DEFAULT 'trainer'::pulseforge.user_role,
            name VARCHAR(150) NOT NULL,
            description TEXT,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE (trainer_id, name)
        );

        CREATE TABLE IF NOT EXISTS pulseforge.workout_template_exercises (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            template_id BIGINT NOT NULL REFERENCES pulseforge.workout_templates(id) ON DELETE CASCADE,
            exercise_id INT NOT NULL REFERENCES pulseforge.exercises(id) ON DELETE RESTRICT,
            position SMALLINT NOT NULL,
            target_sets SMALLINT NOT NULL,
            target_reps SMALLINT NOT NULL,
            UNIQUE (template_id, exercise_id),
            UNIQUE (template_id, position)
        );

        CREATE TABLE IF NOT EXISTS pulseforge.workouts (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            user_id BIGINT NOT NULL REFERENCES pulseforge.users(id) ON DELETE CASCADE,
            based_on_template_id BIGINT REFERENCES pulseforge.workout_templates(id) ON DELETE SET NULL,
            name VARCHAR(150) NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS pulseforge.workout_exercises (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            workout_id BIGINT NOT NULL REFERENCES pulseforge.workouts(id) ON DELETE CASCADE,
            exercise_id INT NOT NULL REFERENCES pulseforge.exercises(id) ON DELETE RESTRICT,
            position SMALLINT NOT NULL,
            target_sets SMALLINT NOT NULL,
            target_reps SMALLINT NOT NULL,
            UNIQUE (workout_id, exercise_id),
            UNIQUE (workout_id, position)
        );

        CREATE TABLE IF NOT EXISTS pulseforge.workout_sessions (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            workout_id BIGINT NOT NULL REFERENCES pulseforge.workouts(id) ON DELETE CASCADE,
            scheduled_date TIMESTAMPTZ NOT NULL,
            status pulseforge.session_status NOT NULL DEFAULT 'planned'::pulseforge.session_status,
            completed_at TIMESTAMPTZ,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS pulseforge.workout_records (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            session_id BIGINT NOT NULL REFERENCES pulseforge.workout_sessions(id) ON DELETE CASCADE,
            exercise_id INT NOT NULL REFERENCES pulseforge.exercises(id) ON DELETE RESTRICT,
            xp_earned INT NOT NULL DEFAULT 0,
            is_suspicious BOOLEAN NOT NULL DEFAULT FALSE,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS pulseforge.workout_sets (
            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            record_id BIGINT NOT NULL REFERENCES pulseforge.workout_records(id) ON DELETE CASCADE,
            set_number SMALLINT NOT NULL,
            reps SMALLINT NOT NULL,
            weight NUMERIC(6,2) NOT NULL DEFAULT 0,
            UNIQUE (record_id, set_number)
        );
        """;

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            logger.LogInformation("Ініціалізація бази даних PulseForge...");
            await EnsureSchemaAndTablesAsync(db);

            await ExerciseSeedData.SeedAsync(db, logger);
            await SeedMedalsAsync(db, logger);
            await SeedAffiliationAsync(db, logger);
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

    private static async Task EnsureSchemaAndTablesAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(SchemaAndTablesSql);

        if (db.Database.GetDbConnection() is NpgsqlConnection npgsqlConnection)
        {
            bool wasClosed = npgsqlConnection.State != ConnectionState.Open;
            if (wasClosed)
            {
                await npgsqlConnection.OpenAsync();
            }

            await npgsqlConnection.ReloadTypesAsync();

            if (wasClosed)
            {
                await npgsqlConnection.CloseAsync();
            }
        }
    }

    private static async Task SeedAffiliationAsync(AppDbContext db, ILogger logger)
    {
        if (!await db.Regions.AnyAsync())
        {
            logger.LogInformation("Засівання довідника регіонів і спортзалів...");
            db.Regions.AddRange(AffiliationSeedData.CreateRegionGraph());
            await db.SaveChangesAsync();
        }

        if (!await db.Divisions.AnyAsync())
        {
            logger.LogInformation("Засівання довідника дивізіонів...");

            // Id призначає база (identity), тому беремо лише назви.
            foreach (var division in AffiliationSeedData.CreateDivisions())
            {
                db.Divisions.Add(new Division { Name = division.Name });
            }

            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedMedalsAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Medals.AnyAsync())
        {
            return;
        }

        logger.LogInformation("Засівання довідника медалей та досягнень...");
        foreach (var seed in MedalRepository.GetDefaultMedals())
        {
            db.Medals.Add(new Medal
            {
                Name = seed.Name,
                Description = seed.Description,
                ConditionType = seed.ConditionType,
                ConditionValue = seed.ConditionValue,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }

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
        await SeedDefaultWorkoutTemplatesAsync(db, logger);
    }

    private static async Task SeedDefaultWorkoutTemplatesAsync(AppDbContext db, ILogger logger)
    {
        if (await db.WorkoutTemplates.AnyAsync())
        {
            return;
        }

        var trainer = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Trainer);
        var exercises = await db.Exercises.OrderBy(e => e.Id).Take(3).ToListAsync();
        if (trainer is null || exercises.Count == 0)
        {
            return;
        }

        logger.LogInformation("Засівання базового шаблону тренування для тренера {TrainerUsername}...", trainer.Username);
        var now = DateTimeOffset.UtcNow;
        var template = new WorkoutTemplate
        {
            TrainerId = trainer.Id,
            TrainerRole = UserRole.Trainer,
            Name = "Базовий силовий спліт (Верх тіла)",
            Description = "Класична програма для розвитку сили грудних м'язів, спини та плечей.",
            CreatedAt = now,
            UpdatedAt = now,
            WorkoutTemplateExercises = exercises
                .Select((ex, idx) => new WorkoutTemplateExercise
                {
                    ExerciseId = ex.Id,
                    Position = (short)(idx + 1),
                    TargetSets = 4,
                    TargetReps = 10,
                })
                .ToList(),
        };

        db.WorkoutTemplates.Add(template);
        await db.SaveChangesAsync();
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
