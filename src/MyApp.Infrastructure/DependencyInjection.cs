using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // --- Денис: DbContext, репозиторії, хешер паролів ---
        // --- Софія: репозиторій шаблонів ---
        // --- Назар: репозиторії тренувань і прогресу ---
        return services;
    }
}