using DbToolkit;
using DbToolkit.Abstractions;
using MariaDbToolkit.Configuration;
using MariaDbToolkit.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MariaDbToolkit;

public static class DependencyInjection
{
    /// <summary>
    /// Enregistre DbToolkit avec MariaDB (ou MySQL) comme moteur.
    /// </summary>
    public static IServiceCollection AddMariaDbToolkit(
        this IServiceCollection services,
        Action<MariaDbOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<IDbConnectionFactory, MariaDbConnectionFactory>();

        return services.AddDbToolkitCore();
    }
}
