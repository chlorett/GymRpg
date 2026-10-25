using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Enums;
using MyApp.Infrastructure.Data;
using MyApp.Infrastructure.Repositories;
using MyApp.Infrastructure.Security;

namespace MyApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default не задано.");

        services.AddDbContext<AppDbContext>(
            options => options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MapEnum<EquipmentType>("equipment_type", "pulseforge")
                    .MapEnum<MedalConditionType>("medal_condition_type", "pulseforge")
                    .MapEnum<SessionStatus>("session_status", "pulseforge")
                    .MapEnum<UserRole>("user_role", "pulseforge")),
            contextLifetime: ServiceLifetime.Transient,
            optionsLifetime: ServiceLifetime.Singleton);

        // --- Денис: DbContext, репозиторії, хешер паролів ---
        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IExerciseRepository, ExerciseRepository>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // --- Софія: репозиторій шаблонів ---
        services.AddTransient<IWorkoutTemplateRepository, WorkoutTemplateRepository>();
        services.AddScoped<IExerciseCatalogService, ExerciseCatalogService>();

        // --- Назар: репозиторії тренувань і прогресу ---
        services.AddTransient<IWorkoutRecordRepository, WorkoutRecordRepository>();
        services.AddTransient<IUserProgressRepository, UserProgressRepository>();

        return services;
    }
}