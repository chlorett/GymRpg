using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Interfaces;
using MyApp.Infrastructure.Data;
using MyApp.Infrastructure.Repositories;

namespace MyApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=pulseforge;Username=postgres;Password=postgres";

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // --- Денис: DbContext, репозиторії, хешер паролів ---

        // --- Софія: репозиторій шаблонів ---

        // --- Назар: репозиторії тренувань і прогресу ---
        services.AddScoped<IWorkoutRecordRepository, WorkoutRecordRepository>();
        services.AddScoped<IUserProgressRepository, UserProgressRepository>();

        return services;
    }
}