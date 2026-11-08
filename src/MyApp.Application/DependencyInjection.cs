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

        // --- Денис: клубна приналежність ---
        services.AddTransient<IAccountAffiliationService, AccountAffiliationService>();

        // --- Софія: шаблони тренувань, профіль користувача ---
        services.AddTransient<IWorkoutTemplateService, WorkoutTemplateService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IWorkoutSessionService, WorkoutSessionService>();

        // --- Назар: відстеження тренувань, RPG-прогрес, лідерборд, медалі та аналітика м'язів ---
        services.AddTransient<Interfaces.IWorkoutTrackingService, Services.WorkoutTrackingService>();
        services.AddTransient<Interfaces.IUserProgressService, Services.UserProgressService>();
        services.AddTransient<Interfaces.ILeaderboardService, Services.LeaderboardService>();
        services.AddTransient<Interfaces.IMedalService, Services.MedalService>();
        services.AddTransient<Interfaces.IMuscleLoadService, Services.MuscleLoadService>();
        return services;
    }
}
