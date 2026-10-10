using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // --- Денис ---
        // services.AddTransient<IAuthService, AuthService>();

        // --- Назар ---
        // services.AddTransient<IWorkoutTrackingService, WorkoutTrackingService>();

        // --- Софія ---
        // services.AddTransient<IWorkoutTemplateService, WorkoutTemplateService>();

        return services;
    }
}
