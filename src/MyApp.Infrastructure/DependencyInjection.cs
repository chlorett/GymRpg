using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Infrastructure;

// Каркас. Наповнює Денис: AppDbContext, хешер, репозиторії.
// Блоки нижче: кожен розкоментовує лише свої рядки.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // --- Денис ---
        // services.AddDbContext<AppDbContext>(..., ServiceLifetime.Transient);
        // services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        // services.AddTransient<IExerciseRepository, ExerciseRepository>();
        // services.AddTransient<IUserRepository, UserRepository>();

        // --- Назар ---
        // services.AddTransient<IWorkoutRecordRepository, WorkoutRecordRepository>();
        // services.AddTransient<IUserProgressRepository, UserProgressRepository>();

        // --- Софія ---
        // services.AddTransient<IWorkoutTemplateRepository, WorkoutTemplateRepository>();

        return services;
    }
}
