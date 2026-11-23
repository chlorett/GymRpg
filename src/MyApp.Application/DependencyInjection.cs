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

        // --- Денис: клубна приналежність, модерація та кваліфікація дивізіонів ---
        services.AddTransient<IAccountAffiliationService, AccountAffiliationService>();
        services.AddTransient<IGymModerationService, GymModerationService>();
        services.AddTransient<IGymStandingsService, GymStandingsService>();
        services.AddTransient<IDivisionQualificationService, DivisionQualificationService>();

        // --- Софія: шаблони тренувань, профіль користувача та сесії ---
        services.AddTransient<IWorkoutTemplateService, WorkoutTemplateService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IWorkoutSessionService, WorkoutSessionService>();

        // --- Назар: відстеження тренувань, RPG-прогрес, лідерборд, медалі, аналітика м'язів, рекорди, відновлення та силові стандарти ---
        services.AddTransient<Interfaces.IWorkoutTrackingService, Services.WorkoutTrackingService>();
        services.AddTransient<Interfaces.IUserProgressService, Services.UserProgressService>();
        services.AddTransient<Interfaces.ILeaderboardService, Services.LeaderboardService>();
        services.AddTransient<Interfaces.IMedalService, Services.MedalService>();
        services.AddTransient<Interfaces.IMuscleLoadService, Services.MuscleLoadService>();
        services.AddTransient<Interfaces.IPersonalRecordService, Services.PersonalRecordService>();
        services.AddTransient<Interfaces.IRecoveryService, Services.RecoveryService>();
        services.AddTransient<Interfaces.IStrengthStandardsService, Services.StrengthStandardsService>();
        return services;
    }
}
