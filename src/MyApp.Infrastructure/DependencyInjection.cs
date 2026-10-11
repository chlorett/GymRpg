using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Interfaces;
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
            options => options.UseNpgsql(connectionString),
            contextLifetime: ServiceLifetime.Transient,
            optionsLifetime: ServiceLifetime.Singleton);

        // --- Денис: DbContext, репозиторії, хешер паролів ---
        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IExerciseRepository, ExerciseRepository>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // --- Софія: репозиторій шаблонів ---

        // --- Назар: репозиторії тренувань і прогресу ---
        services.AddTransient<IWorkoutRecordRepository, WorkoutRecordRepository>();
        services.AddTransient<IUserProgressRepository, UserProgressRepository>();

        return services;
    }
}