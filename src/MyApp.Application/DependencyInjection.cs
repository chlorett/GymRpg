using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;

namespace MyApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // --- Денис: реєстрація репозиторіїв і хешера ---
        services.AddTransient<Interfaces.IAuthService, Services.AuthService>();

        // --- Софія: шаблони тренувань ---
        services.AddTransient<IWorkoutTemplateService, WorkoutTemplateService>();

        // --- Софія: профіль користувача ---
        services.AddScoped<IUserProfileService, UserProfileService>();

        // --- Назар: відстеження тренувань, RPG-прогрес та лідерборд ---
        services.AddTransient<Interfaces.IWorkoutTrackingService, Services.WorkoutTrackingService>();
        services.AddTransient<Interfaces.IUserProgressService, Services.UserProgressService>();
        services.AddTransient<Interfaces.ILeaderboardService, Services.LeaderboardService>();
        return services;
    }
}
