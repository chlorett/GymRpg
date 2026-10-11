using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // --- Денис: реєстрація репозиторіїв і хешера ---
        services.AddTransient<Interfaces.IAuthService, Services.AuthService>();

        // --- Софія: шаблони тренувань ---
        // --- Назар: відстеження тренувань ---
        services.AddTransient<Interfaces.IWorkoutTrackingService, Services.WorkoutTrackingService>();
        return services;
    }
}